using System.Text.Json.Serialization;
using BankAccount.Api;
using JasperFx.CodeGeneration;
using Marten;
using Marten.Events.Daemon.Resiliency;
using Marten.Events.Projections;
using Microsoft.AspNetCore.Http.Json;
using Weasel.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddMarten(options =>
    {
        var schemaName = Environment.GetEnvironmentVariable("SchemaName") ?? "BankAccount";
        options.Events.DatabaseSchemaName = schemaName;
        options.DatabaseSchemaName = schemaName;
        options.Connection(builder.Configuration.GetConnectionString("Postgres")!);
    
        options.UseSystemTextJsonForSerialization(EnumStorage.AsString);

        options.Projections.Add<BankAccountProjection>(ProjectionLifecycle.Inline);
    })
    .OptimizeArtifactWorkflow(TypeLoadMode.Static)
    .UseLightweightSessions()
    .AddAsyncDaemon(DaemonMode.Solo);

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/v1/openapi.json");
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1/openapi.json", "Bank Account API");
        options.RoutePrefix = "";
    });
}

app.UseHttpsRedirection();

await app.StartAsync();

var store = app.Services.GetRequiredService<IDocumentStore>();

var bankService = new BankAccountService(store);

// Example workflow
Console.WriteLine("Bank Account Event Sourcing Example");
Console.WriteLine("====================================");

// Create a new account
var accountId = Guid.NewGuid();
var createResult = await bankService.HandleAsync(new CreateAccountCommand
{
    AccountId = accountId,
    Owner = "John Doe",
    AccountNumber = "12345678",
    InitialBalance = 1000,
    DailyWithdrawalLimit = 500
});

if (createResult.IsError)
{
    Console.WriteLine($"Failed to create account: {createResult.FirstError.Description}");
    return;
}

var account = createResult.Value;
Console.WriteLine($"Account created for {account.Owner} with balance {account.Balance:C}");

// Deposit money
var depositResult = await bankService.HandleAsync(new DepositMoneyCommand
{
    AccountId = accountId,
    Amount = 500,
    Description = "Salary deposit"
});

if (depositResult.IsError)
{
    Console.WriteLine($"Failed to deposit: {depositResult.FirstError.Description}");
}
else
{
    account = depositResult.Value;
    Console.WriteLine($"Deposited 500.00. New balance: {account.Balance:C}");
}

// Withdraw money
var withdrawResult = await bankService.HandleAsync(new WithdrawMoneyCommand
{
    AccountId = accountId,
    Amount = 200,
    Description = "ATM withdrawal"
});

if (withdrawResult.IsError)
{
    Console.WriteLine($"Failed to withdraw: {withdrawResult.FirstError.Description}");
}
else
{
    account = withdrawResult.Value;
    Console.WriteLine($"Withdrew 200.00. New balance: {account.Balance:C}");
}

// Make a reserved (pending) transaction
var pendingWithdrawResult = await bankService.HandleAsync(new WithdrawMoneyCommand
{
    AccountId = accountId,
    Amount = 100,
    Description = "Online purchase - pending",
    IsBooked = false // This is a reserved transaction
});

if (pendingWithdrawResult.IsError)
{
    Console.WriteLine($"Failed to create pending transaction: {pendingWithdrawResult.FirstError.Description}");
}
else
{
    account = pendingWithdrawResult.Value;
    Console.WriteLine($"Created pending withdrawal of 100.00");
    Console.WriteLine($"Booked balance: {account.Balance:C}");
    Console.WriteLine($"Reserved amount: {account.ReservedBalance:C}");
    Console.WriteLine($"Available balance: {account.AvailableBalance:C}");
}

// Charge a monthly fee
var feeResult = await bankService.HandleAsync(new ChargeFeeCommand
{
    AccountId = accountId,
    FeeAmount = 10,
    FeeType = "Monthly maintenance fee"
});

if (feeResult.IsError)
{
    Console.WriteLine($"Failed to charge fee: {feeResult.FirstError.Description}");
}
else
{
    account = feeResult.Value;
    Console.WriteLine($"Charged 10.00 fee. New balance: {account.Balance:C}");
}

// Credit interest
var interestResult = await bankService.HandleAsync(new CreditInterestCommand
{
    AccountId = accountId,
    Rate = 0.001m // 0.1% monthly interest
});

if (interestResult.IsError)
{
    Console.WriteLine($"Failed to credit interest: {interestResult.FirstError.Description}");
}
else
{
    account = interestResult.Value;
    Console.WriteLine($"Credited interest. New balance: {account.Balance:C}");
}

// Close the books (end of month)
Console.WriteLine("\nClosing the period (month)...");
var closePeriodResult = await bankService.HandleAsync(new ClosePeriodCommand
{
    AccountId = accountId,
    ClosingDate = DateTime.UtcNow
});

if (closePeriodResult.IsError)
{
    Console.WriteLine($"Failed to close period: {closePeriodResult.FirstError.Description}");
}
else
{
    var closedAccount = closePeriodResult.Value;
    Console.WriteLine($"Period closed. Final balance: {closedAccount.Balance:C}");
    
    // Get the statement for the closed period
    var statementResult = await bankService.GetStatementAsync(accountId);
    if (!statementResult.IsError)
    {
        var statement = statementResult.Value;
        Console.WriteLine("\nPeriod Statement:");
        Console.WriteLine($"Account: {statement.AccountNumber} ({statement.Owner})");
        Console.WriteLine($"Period: {statement.PeriodStart:d} to {statement.PeriodEnd:d}");
        Console.WriteLine($"Opening Balance: {statement.OpeningBalance:C}");
        Console.WriteLine($"Closing Balance: {statement.ClosingBalance:C}");
        Console.WriteLine($"Available Balance: {statement.AvailableBalance:C}");
        
        Console.WriteLine("\nTransactions:");
        foreach (var transaction in statement.Transactions)
        {
            var status = transaction.IsBooked ? "Booked" : "Pending";
            Console.WriteLine($"{transaction.Timestamp:g} | {transaction.Type} | {transaction.Amount:C} | {status} | {transaction.Description}");
        }
        
        // Show next period ID
        Console.WriteLine($"\nNext Period ID: {statement.NextPeriodId}");
        
        // Demonstrate getting transactions from the new period
        if (statement.NextPeriodId.HasValue)
        {
            // Make a transaction in the new period
            var newPeriodDeposit = await bankService.HandleAsync(new DepositMoneyCommand
            {
                AccountId = statement.NextPeriodId.Value,
                Amount = 300,
                Description = "New period deposit"
            });
            
            if (!newPeriodDeposit.IsError)
            {
                var newPeriodAccount = newPeriodDeposit.Value;
                Console.WriteLine("\nNew Period:");
                Console.WriteLine($"Made deposit of 300.00 in new period");
                Console.WriteLine($"New period balance: {newPeriodAccount.Balance:C}");
                
                // Show that the reserved transaction carried over
                Console.WriteLine($"Reserved amount (carried over): {newPeriodAccount.ReservedBalance:C}");
                Console.WriteLine($"Available balance: {newPeriodAccount.AvailableBalance:C}");
            }
        }
    }
}

// Demonstrate getting full account history
Console.WriteLine("\nRetrieving full account history...");
var accountHistory = await bankService.GetAccountHistoryAsync(accountId);
Console.WriteLine($"Found {accountHistory.Count} periods in account history");

foreach (var statement in accountHistory)
{
    // Format the dates explicitly to ensure they're displayed correctly
    Console.WriteLine($"Period: {statement.PeriodStart:yyyy-MM-dd} to {statement.PeriodEnd:yyyy-MM-dd} | Balance: {statement.ClosingBalance:C}");
}

Console.ReadLine();

await app.StopAsync();

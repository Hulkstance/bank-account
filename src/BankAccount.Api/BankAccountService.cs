using ErrorOr;
using Marten;

namespace BankAccount.Api;

public class BankAccountService(IDocumentStore store)
{
    public async Task<ErrorOr<BankAccount>> HandleAsync(CreateAccountCommand command)
    {
        await using var session = store.LightweightSession();
        
        var @event = new AccountCreatedEvent
        {
            AccountId = command.AccountId,
            Owner = command.Owner,
            AccountNumber = command.AccountNumber,
            InitialBalance = command.InitialBalance,
            DailyWithdrawalLimit = command.DailyWithdrawalLimit
        };

        session.Events.StartStream(command.AccountId, @event);
        await session.SaveChangesAsync();

        var account = await session.LoadAsync<BankAccount>(command.AccountId);
        if (account == null)
            return BankAccountErrors.AccountNotFound;
            
        return account;
    }

    public async Task<ErrorOr<BankAccount>> HandleAsync(DepositMoneyCommand command)
    {
        await using var session = store.LightweightSession();
        
        // Load the account
        var account = await session.LoadAsync<BankAccount>(command.AccountId);
        if (account == null)
            return BankAccountErrors.AccountNotFound;

        if (account.IsClosed)
            return BankAccountErrors.AccountClosed;

        if (command.Amount <= 0)
            return BankAccountErrors.InvalidAmount;

        // Create and append the event
        var @event = new MoneyDepositedEvent
        {
            AccountId = command.AccountId,
            Amount = command.Amount,
            Description = command.Description,
            IsBooked = command.IsBooked
        };

        session.Events.Append(command.AccountId, @event);
        await session.SaveChangesAsync();

        // Reload the account to get the updated state
        account = await session.LoadAsync<BankAccount>(command.AccountId);
        if (account == null)
            return BankAccountErrors.AccountNotFound;
            
        return account;
    }

    public async Task<ErrorOr<BankAccount>> HandleAsync(WithdrawMoneyCommand command)
    {
        await using var session = store.LightweightSession();
        
        // Load the account
        var account = await session.LoadAsync<BankAccount>(command.AccountId);
        if (account == null)
            return BankAccountErrors.AccountNotFound;

        if (account.IsClosed)
            return BankAccountErrors.AccountClosed;

        if (command.Amount <= 0)
            return BankAccountErrors.InvalidAmount;

        // Check if there are sufficient funds
        if (command.IsBooked && account.AvailableBalance < command.Amount)
            return BankAccountErrors.InsufficientFunds;

        // Check daily withdrawal limit (only for booked transactions)
        if (command.IsBooked)
        {
            // Reset the daily tracking if it's a new day
            if (account.LastWithdrawalDate?.Date != DateTime.UtcNow.Date)
            {
                account.TodaysWithdrawals = 0;
                account.LastWithdrawalDate = DateTime.UtcNow;
            }

            if (account.TodaysWithdrawals + command.Amount > account.DailyWithdrawalLimit)
                return BankAccountErrors.WithdrawalLimitExceeded;
        }

        // Create and append the event
        var @event = new MoneyWithdrawnEvent
        {
            AccountId = command.AccountId,
            Amount = command.Amount,
            Description = command.Description,
            IsBooked = command.IsBooked
        };

        session.Events.Append(command.AccountId, @event);
        await session.SaveChangesAsync();

        // Reload the account to get the updated state
        account = await session.LoadAsync<BankAccount>(command.AccountId);
        if (account == null)
            return BankAccountErrors.AccountNotFound;
            
        return account;
    }

    public async Task<ErrorOr<BankAccount>> HandleAsync(TransferMoneyCommand command)
    {
        await using var session = store.LightweightSession();
        
        // Check source account
        var sourceAccount = await session.LoadAsync<BankAccount>(command.AccountId);
        if (sourceAccount == null)
            return BankAccountErrors.AccountNotFound;

        if (sourceAccount.IsClosed)
            return BankAccountErrors.AccountClosed;

        // Check destination account
        var destinationAccount = await session.LoadAsync<BankAccount>(command.DestinationAccountId);
        if (destinationAccount == null)
            return BankAccountErrors.DestinationAccountNotFound;

        if (destinationAccount.IsClosed)
            return BankAccountErrors.AccountClosed;

        if (command.AccountId == command.DestinationAccountId)
            return BankAccountErrors.SameAccount;

        if (command.Amount <= 0)
            return BankAccountErrors.InvalidAmount;

        // Check if there are sufficient funds (for booked transactions)
        if (command.IsBooked && sourceAccount.AvailableBalance < command.Amount)
            return BankAccountErrors.InsufficientFunds;

        // Check daily withdrawal limit (only for booked transactions)
        if (command.IsBooked)
        {
            // Reset the daily tracking if it's a new day
            if (sourceAccount.LastWithdrawalDate?.Date != DateTime.UtcNow.Date)
            {
                sourceAccount.TodaysWithdrawals = 0;
                sourceAccount.LastWithdrawalDate = DateTime.UtcNow;
            }

            if (sourceAccount.TodaysWithdrawals + command.Amount > sourceAccount.DailyWithdrawalLimit)
                return BankAccountErrors.WithdrawalLimitExceeded;
        }

        // Create and append the events
        var sendEvent = new TransferSentEvent
        {
            AccountId = command.AccountId,
            Amount = command.Amount,
            DestinationAccountId = command.DestinationAccountId,
            Description = command.Description,
            IsBooked = command.IsBooked
        };

        var receiveEvent = new TransferReceivedEvent
        {
            AccountId = command.DestinationAccountId,
            Amount = command.Amount,
            SourceAccountId = command.AccountId,
            Description = command.Description,
            IsBooked = command.IsBooked
        };

        session.Events.Append(command.AccountId, sendEvent);
        session.Events.Append(command.DestinationAccountId, receiveEvent);
        await session.SaveChangesAsync();

        // Reload the source account to get the updated state
        sourceAccount = await session.LoadAsync<BankAccount>(command.AccountId);
        if (sourceAccount == null)
            return BankAccountErrors.AccountNotFound;
            
        return sourceAccount;
    }

    public async Task<ErrorOr<BankAccount>> HandleAsync(CreditInterestCommand command)
    {
        await using var session = store.LightweightSession();
        
        // Load the account
        var account = await session.LoadAsync<BankAccount>(command.AccountId);
        if (account == null)
            return BankAccountErrors.AccountNotFound;

        if (account.IsClosed)
            return BankAccountErrors.AccountClosed;

        // Calculate interest amount (simple interest)
        var interestAmount = Math.Round(account.Balance * command.Rate, 2);

        // Create and append the event
        var @event = new InterestCreditedEvent
        {
            AccountId = command.AccountId,
            InterestAmount = interestAmount,
            Rate = command.Rate
        };

        session.Events.Append(command.AccountId, @event);
        await session.SaveChangesAsync();

        // Reload the account to get the updated state
        account = await session.LoadAsync<BankAccount>(command.AccountId);
        if (account == null)
            return BankAccountErrors.AccountNotFound;
            
        return account;
    }

    public async Task<ErrorOr<BankAccount>> HandleAsync(ChargeFeeCommand command)
    {
        await using var session = store.LightweightSession();
        
        // Load the account
        var account = await session.LoadAsync<BankAccount>(command.AccountId);
        if (account == null)
            return BankAccountErrors.AccountNotFound;

        if (account.IsClosed)
            return BankAccountErrors.AccountClosed;

        if (command.FeeAmount <= 0)
            return BankAccountErrors.InvalidAmount;

        // Check if there are sufficient funds
        if (account.AvailableBalance < command.FeeAmount)
            return BankAccountErrors.InsufficientFunds;

        // Create and append the event
        var @event = new FeeChargedEvent
        {
            AccountId = command.AccountId,
            FeeAmount = command.FeeAmount,
            FeeType = command.FeeType
        };

        session.Events.Append(command.AccountId, @event);
        await session.SaveChangesAsync();

        // Reload the account to get the updated state
        account = await session.LoadAsync<BankAccount>(command.AccountId);
        if (account == null)
            return BankAccountErrors.AccountNotFound;
            
        return account;
    }

    public async Task<ErrorOr<BankAccount>> HandleAsync(UpdateLimitCommand command)
    {
        await using var session = store.LightweightSession();
        
        // Load the account
        var account = await session.LoadAsync<BankAccount>(command.AccountId);
        if (account == null)
            return BankAccountErrors.AccountNotFound;

        if (account.IsClosed)
            return BankAccountErrors.AccountClosed;

        if (command.NewDailyWithdrawalLimit <= 0)
            return BankAccountErrors.InvalidAmount;

        // Create and append the event
        var @event = new LimitUpdatedEvent
        {
            AccountId = command.AccountId,
            NewDailyWithdrawalLimit = command.NewDailyWithdrawalLimit
        };

        session.Events.Append(command.AccountId, @event);
        await session.SaveChangesAsync();

        // Reload the account to get the updated state
        account = await session.LoadAsync<BankAccount>(command.AccountId);
        if (account == null)
            return BankAccountErrors.AccountNotFound;
            
        return account;
    }

    public async Task<ErrorOr<BankAccount>> HandleAsync(CloseAccountCommand command)
    {
        await using var session = store.LightweightSession();
        
        // Load the account
        var account = await session.LoadAsync<BankAccount>(command.AccountId);
        if (account == null)
            return BankAccountErrors.AccountNotFound;

        if (account.IsClosed)
            return BankAccountErrors.AccountClosed;

        // Create and append the event
        var @event = new AccountClosedEvent
        {
            AccountId = command.AccountId,
            Reason = command.Reason
        };

        session.Events.Append(command.AccountId, @event);
        await session.SaveChangesAsync();

        // Reload the account to get the updated state
        account = await session.LoadAsync<BankAccount>(command.AccountId);
        if (account == null)
            return BankAccountErrors.AccountNotFound;
            
        return account;
    }

    // "Close the Books" pattern implementation
    public async Task<ErrorOr<BankAccount>> HandleAsync(ClosePeriodCommand command)
    {
        await using var session = store.LightweightSession();
        
        // Load the account
        var account = await session.LoadAsync<BankAccount>(command.AccountId);
        if (account == null)
            return BankAccountErrors.AccountNotFound;

        if (account.IsClosed)
            return BankAccountErrors.AccountClosed;

        var closingDate = command.ClosingDate ?? DateTime.UtcNow;
        
        // Create a new stream ID for the next period
        var newPeriodStreamId = Guid.NewGuid();
        
        // Define the next period time range - making sure we calculate end date correctly
        var nextPeriodStart = new DateTime(closingDate.Year, closingDate.Month, 1).AddMonths(1);
        var nextPeriodEnd = nextPeriodStart.AddMonths(1).AddDays(-1); // Last day of next month

        // Close the current period
        var closePeriodEvent = new PeriodClosedEvent
        {
            AccountId = command.AccountId,
            PeriodStart = account.PeriodStart,
            PeriodEnd = closingDate,
            OpeningBalance = account.Transactions.FirstOrDefault()?.Amount ?? 0,
            ClosingBalance = account.Balance,
            ReservedBalance = account.ReservedBalance,
            AvailableBalance = account.AvailableBalance,
            NewPeriodStreamId = newPeriodStreamId
        };

        // Start a new period with explicit dates
        var startPeriodEvent = new PeriodStartedEvent
        {
            AccountId = newPeriodStreamId,
            PeriodStart = nextPeriodStart,
            PeriodEnd = nextPeriodEnd,
            OpeningBalance = account.Balance,
            OpeningReservedBalance = account.ReservedBalance,
            PreviousPeriodStreamId = command.AccountId
        };

        // Append the events to their respective streams
        session.Events.Append(command.AccountId, closePeriodEvent);
        session.Events.StartStream(newPeriodStreamId, startPeriodEvent);
        await session.SaveChangesAsync();

        // Return the closed period account for reference
        account = await session.LoadAsync<BankAccount>(command.AccountId);
        if (account == null)
            return BankAccountErrors.AccountNotFound;
            
        return account;
    }

    // Helper method to get statement for a specific period
    public async Task<ErrorOr<BankStatement>> GetStatementAsync(Guid accountStreamId)
    {
        await using var session = store.QuerySession();
    
        // Load the account directly from projection
        var account = await session.LoadAsync<BankAccount>(accountStreamId);
        if (account == null)
            return BankAccountErrors.AccountNotFound;

        // For the BankStatement, use the PeriodEnd from the account object directly
        // This should be correct because it was set during the projection from events
        var statement = new BankStatement
        {
            AccountId = account.Id,
            AccountNumber = account.AccountNumber,
            Owner = account.Owner,
            PeriodStart = account.PeriodStart,
            PeriodEnd = account.PeriodEnd,  // Use the account's period end directly
            OpeningBalance = account.Transactions.FirstOrDefault()?.Amount ?? 0,
            ClosingBalance = account.Balance,
            Transactions = account.Transactions.ToList(),
            IsClosed = false,  // We'll update this below
            PreviousPeriodId = account.PreviousPeriodStreamId
        };

        // Get all events for this stream to determine period status and next period ID
        var events = await session.Events.FetchStreamAsync(accountStreamId);
    
        // Find the PeriodClosedEvent if it exists
        var periodClosedEvent = events
            .Select(e => e.Data)
            .OfType<PeriodClosedEvent>()
            .LastOrDefault();
    
        // If period closed event exists, update the statement accordingly
        if (periodClosedEvent != null)
        {
            statement.IsClosed = true;
            statement.NextPeriodId = periodClosedEvent.NewPeriodStreamId;
        }

        return statement;
    }

    // Helper method to get account history (all periods)
    public async Task<List<BankStatement>> GetAccountHistoryAsync(Guid accountId)
    {
        await using var session = store.QuerySession();
    
        var statements = new List<BankStatement>();
        var currentStreamId = accountId;
    
        // Follow the chain of periods
        while (currentStreamId != Guid.Empty)
        {
            var statementResult = await GetStatementAsync(currentStreamId);
            if (statementResult.IsError)
                break;
        
            var statement = statementResult.Value;
            statements.Add(statement);
        
            // Move to the next period if it exists
            if (statement.NextPeriodId.HasValue)
                currentStreamId = statement.NextPeriodId.Value;
            else
                break;
        }
    
        return statements;
    }
    
    public async Task<List<BankStatement>> GetAccountHistoryAsync(string accountNumber)
    {
        await using var session = store.QuerySession();
    
        // Find the account ID by account number
        var account = await session.Query<BankAccount>()
            .FirstOrDefaultAsync(x => x.AccountNumber == accountNumber);
        
        if (account == null)
            return [];
        
        // Now we have the account ID, we can use it to get the history
        return await GetAccountHistoryAsync(account.Id);
    }
}

// Account statement for a specific period
public class BankStatement
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = null!;
    public string Owner { get; set; } = null!;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public List<Transaction> Transactions { get; set; } = [];
    public bool IsClosed { get; set; }
    public Guid? NextPeriodId { get; set; }
    public Guid? PreviousPeriodId { get; set; }
    
    public decimal ReservedBalance => Transactions
        .Where(t => !t.IsBooked)
        .Sum(t => t.Amount);
        
    public decimal AvailableBalance => ClosingBalance - ReservedBalance;
}

using Marten.Events.Aggregation;

namespace BankAccount.Api;

public class BankAccountProjection : SingleStreamProjection<BankAccount>
{
    public BankAccountProjection()
    {
        // Register event handlers
        ProjectEvent<AccountCreatedEvent>((account, @event) => CreateAccount(@event));
        ProjectEvent<MoneyDepositedEvent>((account, @event) => ApplyDeposit(account, @event));
        ProjectEvent<MoneyWithdrawnEvent>((account, @event) => ApplyWithdrawal(account, @event));
        ProjectEvent<TransferSentEvent>((account, @event) => ApplyTransferSent(account, @event));
        ProjectEvent<TransferReceivedEvent>((account, @event) => ApplyTransferReceived(account, @event));
        ProjectEvent<InterestCreditedEvent>((account, @event) => ApplyInterest(account, @event));
        ProjectEvent<FeeChargedEvent>((account, @event) => ApplyFee(account, @event));
        ProjectEvent<LimitUpdatedEvent>((account, @event) => ApplyLimitUpdate(account, @event));
        ProjectEvent<AccountClosedEvent>((account, @event) => ApplyAccountClosed(account, @event));
        ProjectEvent<PeriodClosedEvent>((account, @event) => ApplyPeriodClosed(account, @event));
        ProjectEvent<PeriodStartedEvent>((account, @event) => CreateNewPeriod(@event));
    }

    // Create a new account
    private BankAccount CreateAccount(AccountCreatedEvent @event)
    {
        var account = new BankAccount
        {
            Id = @event.AccountId,
            Owner = @event.Owner,
            AccountNumber = @event.AccountNumber,
            Balance = @event.InitialBalance,
            DailyWithdrawalLimit = @event.DailyWithdrawalLimit,
            CreatedAt = @event.Timestamp,
            
            // Set initial period to current month
            PeriodStart = new DateTime(@event.Timestamp.Year, @event.Timestamp.Month, 1),
            PeriodEnd = new DateTime(@event.Timestamp.Year, @event.Timestamp.Month, 1).AddMonths(1).AddDays(-1)
        };

        AddTransaction(account, "Opening Balance", @event.InitialBalance, "Initial Deposit", true);
        return account;
    }

    // Handle deposits
    private BankAccount ApplyDeposit(BankAccount account, MoneyDepositedEvent @event)
    {
        if (@event.IsBooked)
        {
            account.Balance += @event.Amount;
        }
        else
        {
            account.ReservedBalance += @event.Amount;
        }

        AddTransaction(account, "Deposit", @event.Amount, @event.Description, @event.IsBooked);
        return account;
    }

    // Handle withdrawals
    private BankAccount ApplyWithdrawal(BankAccount account, MoneyWithdrawnEvent @event)
    {
        if (@event.IsBooked)
        {
            account.Balance -= @event.Amount;
            UpdateDailyWithdrawalTracking(account, @event.Amount, @event.Timestamp);
        }
        else
        {
            account.ReservedBalance += @event.Amount;
        }

        AddTransaction(account, "Withdrawal", -@event.Amount, @event.Description, @event.IsBooked);
        return account;
    }

    // Handle transfers sent
    private BankAccount ApplyTransferSent(BankAccount account, TransferSentEvent @event)
    {
        if (@event.IsBooked)
        {
            account.Balance -= @event.Amount;
            UpdateDailyWithdrawalTracking(account, @event.Amount, @event.Timestamp);
        }
        else
        {
            account.ReservedBalance += @event.Amount;
        }

        AddTransaction(account, "Transfer Sent", -@event.Amount, @event.Description, @event.IsBooked);
        return account;
    }

    // Handle transfers received
    private BankAccount ApplyTransferReceived(BankAccount account, TransferReceivedEvent @event)
    {
        if (@event.IsBooked)
        {
            account.Balance += @event.Amount;
        }
        else
        {
            account.ReservedBalance -= @event.Amount;
        }

        AddTransaction(account, "Transfer Received", @event.Amount, @event.Description, @event.IsBooked);
        return account;
    }

    // Handle interest credits
    private BankAccount ApplyInterest(BankAccount account, InterestCreditedEvent @event)
    {
        account.Balance += @event.InterestAmount;
        AddTransaction(account, "Interest", @event.InterestAmount, $"Interest at {@event.Rate:P2}", true);
        return account;
    }

    // Handle fees
    private BankAccount ApplyFee(BankAccount account, FeeChargedEvent @event)
    {
        account.Balance -= @event.FeeAmount;
        AddTransaction(account, "Fee", -@event.FeeAmount, @event.FeeType, true);
        return account;
    }

    // Handle limit updates
    private BankAccount ApplyLimitUpdate(BankAccount account, LimitUpdatedEvent @event)
    {
        account.DailyWithdrawalLimit = @event.NewDailyWithdrawalLimit;
        return account;
    }

    // Handle account closure
    private BankAccount ApplyAccountClosed(BankAccount account, AccountClosedEvent @event)
    {
        account.IsClosed = true;
        account.ClosedAt = @event.Timestamp;
        return account;
    }

    // Handle period closure
    private BankAccount ApplyPeriodClosed(BankAccount account, PeriodClosedEvent @event)
    {
        account.PeriodEnd = @event.PeriodEnd;
        return account;
    }

    // Create a new period
    private BankAccount CreateNewPeriod(PeriodStartedEvent @event)
    {
        var account = new BankAccount
        {
            Id = @event.AccountId,
            PeriodStart = @event.PeriodStart,
            PeriodEnd = @event.PeriodEnd,
            Balance = @event.OpeningBalance,
            ReservedBalance = @event.OpeningReservedBalance,
            PreviousPeriodStreamId = @event.PreviousPeriodStreamId
        };
    
        AddTransaction(account, "Opening Balance", @event.OpeningBalance, "Period Opening Balance", true);
        return account;
    }

    // Helper method to add transactions
    private void AddTransaction(BankAccount account, string type, decimal amount, string description, bool isBooked)
    {
        account.Transactions.Add(new Transaction
        {
            Type = type,
            Amount = amount,
            Description = description,
            Timestamp = DateTime.UtcNow,
            IsBooked = isBooked
        });
    }

    // Helper method to track daily withdrawals
    private void UpdateDailyWithdrawalTracking(BankAccount account, decimal withdrawalAmount, DateTime timestamp)
    {
        // Reset daily withdrawal tracking if it's a new day
        if (account.LastWithdrawalDate == null || account.LastWithdrawalDate?.Date != timestamp.Date)
        {
            account.TodaysWithdrawals = 0;
            account.LastWithdrawalDate = timestamp;
        }

        account.TodaysWithdrawals += withdrawalAmount;
    }
}

namespace BankAccount.Api;

// Base event interface
public interface IBankAccountEvent
{
    Guid AccountId { get; }
    DateTime Timestamp { get; }
}

// Base event class
public abstract class BankAccountEvent : IBankAccountEvent
{
    public Guid AccountId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class AccountCreatedEvent : BankAccountEvent
{
    public string Owner { get; set; } = null!;
    public string AccountNumber { get; set; } = null!;
    public decimal InitialBalance { get; set; }
    public decimal DailyWithdrawalLimit { get; set; }
}

public class MoneyDepositedEvent : BankAccountEvent
{
    public decimal Amount { get; set; }
    public string Description { get; set; } = null!;
    public bool IsBooked { get; set; } = true; // Indicates if transaction is booked or reserved
}

public class MoneyWithdrawnEvent : BankAccountEvent
{
    public decimal Amount { get; set; }
    public string Description { get; set; } = null!;
    public bool IsBooked { get; set; } = true; // Indicates if transaction is booked or reserved
}

public class TransferSentEvent : BankAccountEvent
{
    public decimal Amount { get; set; }
    public Guid DestinationAccountId { get; set; }
    public string Description { get; set; } = null!;
    public bool IsBooked { get; set; } = true; // Indicates if transaction is booked or reserved
}

public class TransferReceivedEvent : BankAccountEvent
{
    public decimal Amount { get; set; }
    public Guid SourceAccountId { get; set; }
    public string Description { get; set; } = null!;
    public bool IsBooked { get; set; } = true; // Indicates if transaction is booked or reserved
}

public class InterestCreditedEvent : BankAccountEvent
{
    public decimal InterestAmount { get; set; }
    public decimal Rate { get; set; }
}

public class FeeChargedEvent : BankAccountEvent
{
    public decimal FeeAmount { get; set; }
    public string FeeType { get; set; } = null!;
}

public class LimitUpdatedEvent : BankAccountEvent
{
    public decimal NewDailyWithdrawalLimit { get; set; }
}

public class AccountClosedEvent : BankAccountEvent
{
    public string Reason { get; set; } = null!;
}

// Event for "Close the Books" pattern
public class PeriodClosedEvent : BankAccountEvent
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal ReservedBalance { get; set; } // Balance of non-booked transactions
    public decimal AvailableBalance { get; set; } // ClosingBalance - ReservedBalance
    public Guid NewPeriodStreamId { get; set; } // Reference to the next period's stream
}

// Initial event for a new period
public class PeriodStartedEvent : BankAccountEvent
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal OpeningReservedBalance { get; set; }
    public Guid PreviousPeriodStreamId { get; set; } // Reference to the previous period's stream
}

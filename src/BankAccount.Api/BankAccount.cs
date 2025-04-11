namespace BankAccount.Api;

public class Transaction
{
    public DateTime Timestamp { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = null!;
    public string Type { get; set; } = null!;
    public bool IsBooked { get; set; }
}

public class BankAccount
{
    public Guid Id { get; set; }
    public string Owner { get; set; } = null!;
    public string AccountNumber { get; set; } = null!;
    public decimal Balance { get; set; }
    public decimal ReservedBalance { get; set; } // For non-booked transactions
    public decimal AvailableBalance => Balance - ReservedBalance;
    public decimal DailyWithdrawalLimit { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public Guid? PreviousPeriodStreamId { get; set; }
    public List<Transaction> Transactions { get; set; } = [];

    // These track today's withdrawals for limit enforcement
    public decimal TodaysWithdrawals { get; set; }
    public DateTime? LastWithdrawalDate { get; set; }
}
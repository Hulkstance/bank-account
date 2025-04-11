namespace BankAccount.Api;

// Command base class
public abstract class BankAccountCommand
{
    public Guid AccountId { get; set; }
}

public class CreateAccountCommand : BankAccountCommand
{
    public string Owner { get; set; } = null!;
    public string AccountNumber { get; set; } = null!;
    public decimal InitialBalance { get; set; }
    public decimal DailyWithdrawalLimit { get; set; } = 1000; // Default limit
}

public class DepositMoneyCommand : BankAccountCommand
{
    public decimal Amount { get; set; }
    public string Description { get; set; } = null!;
    public bool IsBooked { get; set; } = true;
}

public class WithdrawMoneyCommand : BankAccountCommand
{
    public decimal Amount { get; set; }
    public string Description { get; set; } = null!;
    public bool IsBooked { get; set; } = true;
}

public class TransferMoneyCommand : BankAccountCommand
{
    public Guid DestinationAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = null!;
    public bool IsBooked { get; set; } = true;
}

public class CreditInterestCommand : BankAccountCommand
{
    public decimal Rate { get; set; }
}

public class ChargeFeeCommand : BankAccountCommand
{
    public decimal FeeAmount { get; set; }
    public string FeeType { get; set; } = null!;
}

public class UpdateLimitCommand : BankAccountCommand
{
    public decimal NewDailyWithdrawalLimit { get; set; }
}

public class CloseAccountCommand : BankAccountCommand
{
    public string Reason { get; set; } = null!;
}

// Command for "Close the Books"
public class ClosePeriodCommand : BankAccountCommand
{
    public DateTime? ClosingDate { get; set; } // If null, use current date
}

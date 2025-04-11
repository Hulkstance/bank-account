using ErrorOr;

namespace BankAccount.Api;

public static class BankAccountErrors
{
    public static readonly Error AccountNotFound = Error.NotFound("Account.NotFound", "The bank account was not found.");
    public static readonly Error InsufficientFunds = Error.Failure("Account.InsufficientFunds", "Insufficient funds to complete the transaction.");
    public static readonly Error WithdrawalLimitExceeded = Error.Failure("Account.WithdrawalLimitExceeded", "Daily withdrawal limit has been exceeded.");
    public static readonly Error AccountClosed = Error.Failure("Account.Closed", "Cannot perform operations on a closed account.");
    public static readonly Error InvalidAmount = Error.Validation("Account.InvalidAmount", "The amount must be greater than zero.");
    public static readonly Error DestinationAccountNotFound = Error.NotFound("Account.DestinationNotFound", "The destination account was not found.");
    public static readonly Error SameAccount = Error.Validation("Account.SameAccount", "Cannot transfer money to the same account.");
}

# 💰 Bank Account — Event Sourcing with Marten

This repo is a simple bank account showcase using Event Sourcing with Marten. Yes, I'm aware of [Oskar Dudycz's article](https://event-driven.io/en/bank_account_event_sourcing/) explaining why a bank account isn't the best example for event sourcing. But despite that, I still find it an interesting and relatable domain to experiment with.

## 🤔 Why this project?

Oskar makes a valid point about why bank accounts aren't the best domain for learning event sourcing. He's basically saying that we think we know how this business domain works, but in reality, it's different than we think.

Here's what he points out in [this article](https://event-driven.io/en/closing_the_books_in_practice/) about how this business domain is actually different from what we might expect:
- There's no database transaction between multiple bank accounts when transferring money.
- A Bank Account is also not an entity or transactional boundary.
- In accounting, we work with lifecycles, such as an Accounting Month.

Another thing, there's a real concern about event streams being too long to be practical to use. "Close the books" pattern is a way to get you to shorter streams by saying "we'll model account 123 for month periods" instead of "model account 123 for its entire lifetime". So at the beginning of the month you'd roll over the existing balance of an account to a new event that starts a new stream for the new month.

One more thing, you can do a transaction with Marten that spans two different accounts modeled by two different event streams. You can't do real transactions across aggregates in some popular event stores, so there's a lot of event sourcing "best practices" that are really just working around limitations of those tools. We get a lot of benefits of just building on top of PostgreSQL.

> For example, if you're modeling a money transfer from Account A to Account B, you can append an event to both streams, and either both changes succeed or neither does. That's an actual atomic transaction.

## Diagrams

```mermaid
flowchart TD
    subgraph Period1["Period 1 (April 2025)"]
        AC[Account Created] --> MD1[Money Deposited]
        MD1 --> MW1[Money Withdrawn]
        MW1 --> PWD[Pending Withdrawal]
        PWD --> FC[Fee Charged]
        FC --> IC[Interest Credited]
        IC --> CBP["Close the Books Pattern"]
    end
    
    subgraph Period2["Period 2 (May 2025)"]
        PS[Period Started] --> CarryFwd[Carry Forward Balance]
        CarryFwd --> CarryRes[Carry Forward Reserved Transactions]
        CarryRes --> MD2[Money Deposited]
        MD2 --> CC["Continue Cycle..."]
        CC --> CBP2["Close the Books Pattern"]
    end
    
    CBP -->|Creates New Stream| PS
    CBP -->|Stores Final State| SP1[Statement Period 1]
    CBP2 -->|Creates New Stream| PS2[Period 3 Started]
    CBP2 -->|Stores Final State| SP2[Statement Period 2]
    
    subgraph History["Account History"]
        SP1 -->|NextPeriodId| SP2
        SP2 -->|NextPeriodId| SP3[Subsequent Periods...]
        SP2 -.->|PreviousPeriodId| SP1
        SP3 -.->|PreviousPeriodId| SP2
    end
    
    classDef periodBox fill:#f9f9f9,stroke:#333,stroke-width:1px
    classDef historyBox fill:#e6f7ff,stroke:#333,stroke-width:1px
    class Period1 periodBox
    class Period2 periodBox
    class History historyBox
```

```mermaid
sequenceDiagram
    participant Client
    participant BankService
    participant EventStore
    participant Period1 as Period 1 Stream
    participant Period2 as Period 2 Stream

    Client->>BankService: CreateAccount
    BankService->>Period1: Append AccountCreatedEvent
    Note right of Period1: Account with<br/>initial balance<br/>created

    Client->>BankService: Deposit Money
    BankService->>Period1: Append MoneyDepositedEvent
    Note right of Period1: Balance updated

    Client->>BankService: Withdraw Money
    BankService->>Period1: Append MoneyWithdrawnEvent
    Note right of Period1: Balance updated

    Client->>BankService: Create Pending Transaction
    BankService->>Period1: Append MoneyWithdrawnEvent (isBooked=false)
    Note right of Period1: Reserved balance<br/>updated

    Client->>BankService: Charge Fee
    BankService->>Period1: Append FeeChargedEvent
    Note right of Period1: Balance updated

    Client->>BankService: Credit Interest
    BankService->>Period1: Append InterestCreditedEvent
    Note right of Period1: Balance updated

    Client->>BankService: Close Period (ClosePeriodCommand)
    Note over BankService: Generate new period ID
    Note over BankService: Calculate next period dates
    BankService->>Period1: Append PeriodClosedEvent
    Note right of Period1: Store final balance<br/>Store next period ID

    BankService->>Period2: Start new stream with PeriodStartedEvent
    Note right of Period2: Initial balance = previous closing balance<br/>Reserved balance carried forward<br/>Store previous period ID

    Client->>BankService: Get Period 1 Statement
    BankService->>Period1: Retrieve events
    Period1-->>BankService: Events
    BankService-->>Client: Statement with Next Period ID

    Client->>BankService: Deposit to Period 2
    BankService->>Period2: Append MoneyDepositedEvent
    Note right of Period2: Balance updated<br/>Reserved balance unchanged

    Client->>BankService: Get Account History
    BankService->>Period1: Get Statement
    Period1-->>BankService: Statement
    BankService->>Period2: Get Statement (using Next Period ID)
    Period2-->>BankService: Statement
    BankService-->>Client: Complete account history
```
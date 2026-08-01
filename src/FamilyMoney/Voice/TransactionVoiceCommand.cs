namespace FamilyMoney.Voice;

public sealed record TransactionVoiceCommand(TransactionVoiceCommandKind Kind, string Value);

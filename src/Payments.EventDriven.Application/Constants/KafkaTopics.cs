namespace Payments.EventDriven.Application.Constants;

public static class KafkaTopics
{
    public const string PaymentCreated = "payment-created";
    public const string PaymentCreatedDlq = "payment-created-dlq";
    public const string PaymentDeleted = "payment-deleted";
    public const string PaymentDeletedDlq = "payment-deleted-dlq";
}

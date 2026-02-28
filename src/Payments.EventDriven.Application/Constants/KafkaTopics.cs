namespace Payments.EventDriven.Application.Constants;

public static class KafkaTopics
{
    public const string PaymentCreated = "payment-created";
    public const string PaymentCreatedDlq = "payment-created-dlq";
}

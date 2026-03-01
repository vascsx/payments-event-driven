namespace Payments.EventDriven.Domain.Enums;

/// <summary>
/// Tipos de pagamento suportados pelo sistema
/// </summary>
public enum PaymentType
{
    Default = 0,
    Darf = 1,    // Documento de Arrecadação de Receitas Federais
    Darj = 2     // Documento de Arrecadação do Estado do Rio de Janeiro
}

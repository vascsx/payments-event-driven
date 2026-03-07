export function paymentFactory(overrides = {}) {
    return {
        amount: 10.00,
        currency: "USD",
        ...overrides
    };
}
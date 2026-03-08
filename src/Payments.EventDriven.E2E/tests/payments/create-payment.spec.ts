import { expect } from '@playwright/test';
import { test } from '../fixtures/payment-fixture';
import { paymentFactory } from '../factories/payment-factory';
import { createPayment, createAndWaitProcessing } from '../helpers/payment-helper';
import { createdPayments } from '../utils/cleanup';

test.describe('Payment creation', () => {
    test('should create a payment and return a valid GUID when all required fields are provided', async ({ request }) => {
        const paymentData = paymentFactory();
        const response = await createPayment(request, paymentData);

        expect(response.status()).toBe(201);

        const body = await response.json();
        expect(body.id).toEqual(
            expect.stringMatching(
                /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i
            )
        );

        createdPayments.push(body.id);
    });

    test('should transition payment from Pending to Processed status automatically', async ({ request }) => {
        const paymentData = paymentFactory({ amount: 12.00, currency: "BRL" });

        const { payment } = await createAndWaitProcessing(request, paymentData);

        expect(payment.status).toBe('Processed');
    });
});

test.describe('Validation errors', () => {
    test('should reject payment creation with 400 when amount is negative', async ({ request }) => {
        const paymentData = paymentFactory({ amount: -10.00 });
        const response = await createPayment(request, paymentData);

        expect(response.status()).toBe(400);
        expect((await response.json()).errors.Amount[0]).toBe("Amount must be greater than 0.");
    });

    test('should reject payment creation with 400 when currency field is empty', async ({ request }) => {
        const paymentData = paymentFactory({ currency: "" });
        const response = await createPayment(request, paymentData);

        expect(response.status()).toBe(400);
        expect((await response.json()).errors.Currency[0]).toBe("The Currency field is required.");
    });

    test('should reject payment creation with 400 when amount has more than 2 decimal places', async ({ request }) => {
        const paymentData = paymentFactory({ amount: 10.123, currency: "BRL" });
        const response = await createPayment(request, paymentData);

        expect(response.status()).toBe(400);
        expect((await response.json()).detail).toBe("Amount cannot have more than 2 decimal places.");
    });

    test('should transition payment from Pending to Processed status automatically', async ({ request }) => {
        const paymentData = paymentFactory({ amount: 12.00, currency: "BRL" });

        const { payment } = await createAndWaitProcessing(request, paymentData);

        expect(payment.status).toBe('Processed');
    });

    test('should reject payment creation with 400 when currency is not a valid ISO 4217 code', async ({ request }) => {
        const paymentData = paymentFactory({ currency: "INVALID" });
        const response = await createPayment(request, paymentData);

        expect(response.status()).toBe(400);
        expect((await response.json()).errors.Currency[0]).toContain("Currency must be a valid ISO 4217 code");
    });

    test('should reject payment creation with 400 when amount is zero', async ({ request }) => {
        const paymentData = paymentFactory({ amount: 0 });
        const response = await createPayment(request, paymentData);

        expect(response.status()).toBe(400);
        expect((await response.json()).errors.Amount[0]).toBe("Amount must be greater than 0.");
    });

    test('should reject payment creation with 400 when request body is null', async ({ request }) => {
        const response = await request.post('/api/payments', {
            headers: { 'Content-Type': 'application/json' },
            data: null
        });

        expect(response.status()).toBe(400);
    });

    test('should reject payment creation with 400 when JSON format is invalid', async ({ request }) => {
        const response = await request.post('/api/payments', {
            headers: { 'Content-Type': 'application/json' },
            data: '{ invalid json }'
        });

        expect(response.status()).toBe(400);
    });
});

test('should accept payment creation when amount is extremely large', async ({ request }) => {
    const paymentData = paymentFactory({ amount: 999999999999.99 });
    const response = await createPayment(request, paymentData);

    expect(response.status()).toBe(201);
    
    const body = await response.json();
    createdPayments.push(body.id);
});

test('should accept payment creation when amount has exactly 2 decimal places', async ({ request }) => {
    const paymentData = paymentFactory({ amount: 10.99, currency: "USD" });
    const response = await createPayment(request, paymentData);

    expect(response.status()).toBe(201);
    const body = await response.json();
    createdPayments.push(body.id);
});


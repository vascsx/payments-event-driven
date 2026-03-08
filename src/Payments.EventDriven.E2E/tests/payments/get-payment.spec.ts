import { expect } from '@playwright/test';
import { test } from '../fixtures/payment-fixture';
import { paymentFactory } from '../factories/payment-factory';
import { createPayment, getPayment, createAndWaitProcessing } from '../helpers/payment-helper';
import { createdPayments } from '../utils/cleanup';

test('should return payment details with 200 when payment exists', async ({ request }) => {
    const paymentData = paymentFactory({ amount: 50.00, currency: "USD" });
    const createResponse = await createPayment(request, paymentData);

    expect(createResponse.status()).toBe(201);
    const { id } = await createResponse.json();
    createdPayments.push(id);

    const getResponse = await getPayment(request, id);

    expect(getResponse.status()).toBe(200);

    const payment = await getResponse.json();
    expect(payment.id).toBe(id);
    expect(payment.amount).toBe(50.00);
    expect(payment.currency).toBe("USD");
    expect(payment.status).toBeDefined();
});

test('should return 404 when attempting to get payment with non-existent ID', async ({ request }) => {
    const nonExistentId = '00000000-0000-0000-0000-000000000000';
    const response = await getPayment(request, nonExistentId);

    expect(response.status()).toBe(404);
});

test('should return 404 when payment ID format is invalid', async ({ request }) => {
    const invalidId = 'not-a-valid-guid';
    const response = await request.get(`/api/payments/${invalidId}`);

    expect(response.status()).toBe(404);
});

test('should return payment in Pending status immediately after creation', async ({ request }) => {
    const paymentData = paymentFactory({ amount: 75.00, currency: "EUR" });
    const createResponse = await createPayment(request, paymentData);

    expect(createResponse.status()).toBe(201);
    const { id } = await createResponse.json();
    createdPayments.push(id);

    const getResponse = await getPayment(request, id);

    expect(getResponse.status()).toBe(200);

    const payment = await getResponse.json();
    expect(payment.id).toBe(id);
    expect(payment.status).toBe('Pending');
});

test('should return payment in Processed status after being processed', async ({ request }) => {
    const paymentData = paymentFactory({ amount: 125.00, currency: "BRL" });

    const { id, payment } = await createAndWaitProcessing(request, paymentData);

    expect(payment.status).toBe('Processed');
    expect(payment.id).toBe(id);
});

test('should return payment with valid status regardless of processing state', async ({ request }) => {
    const paymentData = paymentFactory({ amount: 25.00, currency: "GBP" });
    const createResponse = await createPayment(request, paymentData);

    expect(createResponse.status()).toBe(201);
    const { id } = await createResponse.json();
    createdPayments.push(id);

    const getResponse = await getPayment(request, id);
    expect(getResponse.status()).toBe(200);

    const payment = await getResponse.json();
    expect(payment.id).toBe(id);
    expect(['Pending', 'Processed', 'Failed']).toContain(payment.status);
});

import { expect } from '@playwright/test';
import { test } from '../fixtures/payment-fixture';
import { paymentFactory } from '../factories/payment-factory';
import { createPayment, getPayment } from '../helpers/payment-helper';
import { waitForStatus } from '../utils/polling';
import { createdPayments } from '../utils/cleanup';

test('CT01 - Valid Payment Creation', async ({ request }) => {
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

test('CT02 - Reject Payment with Invalid Amount', async ({ request }) => {
    const paymentData = paymentFactory({ amount: -10.00 });
    const response = await createPayment(request, paymentData);
    
    expect(response.status()).toBe(400);
    expect((await response.json()).errors.Amount[0]).toBe("Amount must be greater than 0.");
});

test('CT03 - Reject Payment with Missing Currency', async ({ request }) => {
    const paymentData = paymentFactory({ currency: "" });
    const response = await createPayment(request, paymentData);
    
    expect(response.status()).toBe(400);
    expect((await response.json()).errors.Currency[0]).toBe("The Currency field is required.");
});

test('CT04 - Reject Payment with Invalid Monetary Precision', async ({ request }) => {
    const paymentData = paymentFactory({ amount: 10.123, currency: "BRL" });
    const response = await createPayment(request, paymentData);
    
    expect(response.status()).toBe(400);
    expect((await response.json()).detail).toBe("Amount cannot have more than 2 decimal places.");
});

test('CT05 - Processing a Pending Payment', async ({ request }) => {
    const paymentData = paymentFactory({ amount: 12.00, currency: "BRL" });
    const createResponse = await createPayment(request, paymentData);
    
    expect(createResponse.status()).toBe(201);

    const { id } = await createResponse.json();
    expect(id).toBeDefined();
    
    createdPayments.push(id);

    const payment = await waitForStatus(
        () => getPayment(request, id),
        (payment: any) => payment.status,
        'Processed'
    );
    
    expect(payment.status).toBe('Processed');
});


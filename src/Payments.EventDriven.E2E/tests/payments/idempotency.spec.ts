import { expect } from '@playwright/test';
import { test } from '../fixtures/payment-fixture';
import { paymentFactory } from '../factories/payment-factory';
import { createPayment } from '../helpers/payment-helper';
import { createdPayments } from '../utils/cleanup';

test('should accept payment creation when idempotency key is provided', async ({ request }) => {
    const idempotencyKey = `test-${Date.now()}-${Math.random()}`;
    const paymentData = paymentFactory({ 
        amount: 100.00, 
        currency: "BRL",
        idempotencyKey
    });
    
    const response = await createPayment(request, paymentData);
    
    expect(response.status()).toBe(201);

    const body = await response.json();
    expect(body.id).toBeDefined();
    
    createdPayments.push(body.id);
});

test('should return same payment ID when duplicate request is sent with same idempotency key', async ({ request }) => {
    const idempotencyKey = `test-duplicate-${Date.now()}-${Math.random()}`;
    const paymentData = paymentFactory({ 
        amount: 150.00, 
        currency: "EUR",
        idempotencyKey
    });
    
    const firstResponse = await createPayment(request, paymentData);
    expect(firstResponse.status()).toBe(201);
    
    const firstBody = await firstResponse.json();
    expect(firstBody.id).toBeDefined();
    createdPayments.push(firstBody.id);
    
    const secondResponse = await createPayment(request, paymentData);
    
    expect([201, 409]).toContain(secondResponse.status());
    
    if (secondResponse.status() === 201) {
        const secondBody = await secondResponse.json();
        expect(secondBody.id).toBe(firstBody.id); 
    }
});

test('should handle conflict when different payment data is sent with same idempotency key', async ({ request }) => {
    const idempotencyKey = `test-conflict-${Date.now()}-${Math.random()}`;
    
    const firstPaymentData = paymentFactory({ 
        amount: 100.00, 
        currency: "BRL",
        idempotencyKey
    });
    const firstResponse = await createPayment(request, firstPaymentData);
    expect(firstResponse.status()).toBe(201);
    
    const firstBody = await firstResponse.json();
    createdPayments.push(firstBody.id);
    
    const secondPaymentData = paymentFactory({ 
        amount: 200.00, 
        currency: "USD", 
        idempotencyKey 
    });
    const secondResponse = await createPayment(request, secondPaymentData);
    
    expect([201, 400, 409]).toContain(secondResponse.status());
    
    if (secondResponse.status() === 201) {
        const secondBody = await secondResponse.json();
        expect(secondBody.id).toBe(firstBody.id);
    }
});

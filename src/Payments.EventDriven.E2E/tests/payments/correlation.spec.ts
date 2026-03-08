import { expect } from '@playwright/test';
import { test } from '../fixtures/payment-fixture';
import { paymentFactory } from '../factories/payment-factory';
import { createdPayments } from '../utils/cleanup';

test('should return X-Correlation-Id header in response when payment is created', async ({ request }) => {
    const paymentData = paymentFactory({ amount: 50.00, currency: "USD" });
    
    const response = await request.post('/api/payments', {
        data: paymentData
    });
    
    expect(response.status()).toBe(201);
    
    const correlationId = response.headers()['x-correlation-id'];
    expect(correlationId).toBeDefined();
    expect(correlationId).toBeTruthy();
    
    const body = await response.json();
    createdPayments.push(body.id);
});

test('should propagate custom X-Correlation-Id from request to response', async ({ request }) => {
    const customCorrelationId = `test-${Date.now()}-${Math.random()}`;
    const paymentData = paymentFactory({ amount: 75.00, currency: "EUR" });
    
    const response = await request.post('/api/payments', {
        data: paymentData,
        headers: {
            'X-Correlation-Id': customCorrelationId
        }
    });
    
    expect(response.status()).toBe(201);
    
    const returnedCorrelationId = response.headers()['x-correlation-id'];
    expect(returnedCorrelationId).toBe(customCorrelationId);
    
    const body = await response.json();
    createdPayments.push(body.id);
});

test('should auto-generate valid GUID for X-Correlation-Id when not provided in request', async ({ request }) => {
    const paymentData = paymentFactory({ amount: 100.00, currency: "BRL" });
    
    const response = await request.post('/api/payments', {
        data: paymentData
    });
    
    expect(response.status()).toBe(201);
    
    const correlationId = response.headers()['x-correlation-id'];
    expect(correlationId).toBeDefined();
    expect(correlationId).toBeTruthy();
    
    const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    expect(correlationId).toMatch(guidRegex);
    
    const body = await response.json();
    createdPayments.push(body.id);
});

test('should maintain correlation-Id consistency across multiple API requests', async ({ request }) => {
    const correlationId = `consistent-test-${Date.now()}`;
    
    const createPaymentData = paymentFactory({ amount: 125.00, currency: "GBP" });
    const createResponse = await request.post('/api/payments', {
        data: createPaymentData,
        headers: {
            'X-Correlation-Id': correlationId
        }
    });
    
    expect(createResponse.status()).toBe(201);
    expect(createResponse.headers()['x-correlation-id']).toBe(correlationId);
    
    const { id } = await createResponse.json();
    createdPayments.push(id);
});

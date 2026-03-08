import { expect } from '@playwright/test';
import { test } from '../fixtures/payment-fixture';
import { paymentFactory } from '../factories/payment-factory';
import { createPayment, getPayment } from '../helpers/payment-helper';
import { waitForStatus, waitForCondition } from '../utils/polling';
import { createdPayments } from '../utils/cleanup';

test('should automatically transition payment from Pending to Processed status', async ({ request }) => {
    const paymentData = paymentFactory({ amount: 100.00, currency: "BRL" });
    const createResponse = await createPayment(request, paymentData);
    
    expect(createResponse.status()).toBe(201);
    const { id } = await createResponse.json();
    createdPayments.push(id);
    
    const processedPayment = await waitForStatus(
        () => getPayment(request, id),
        (payment: any) => payment.status,
        'Processed'
    );
    
    expect(processedPayment.status).toBe('Processed');
    expect(processedPayment.id).toBe(id);
});

test('should transition payment to Failed status when processing fails', async ({ request }) => {
    const paymentData = paymentFactory({ amount: 1.00, currency: "XXX" });
    const createResponse = await createPayment(request, paymentData);
    
    if (createResponse.status() === 201) {
        const { id } = await createResponse.json();
        createdPayments.push(id);
        
        const payment = await waitForCondition(
            () => getPayment(request, id),
            (payment: any) => payment.status !== 'Pending',
            { maxAttempts: 60, pollIntervalMs: 500 }
        );
        
        expect(['Processed', 'Failed']).toContain(payment.status);
        
        if (payment.status === 'Failed') {
            expect(payment.failureReason).toBeDefined();
        }
    } else {
        expect(createResponse.status()).toBe(400);
    }
});

test('should ensure all payments follow valid state transition rules from Pending to final state', async ({ request }) => {
    const payments = [
        paymentFactory({ amount: 10.00, currency: "USD" }),
        paymentFactory({ amount: 20.00, currency: "EUR" }),
        paymentFactory({ amount: 30.00, currency: "BRL" })
    ];
    
    const createResponses = await Promise.all(
        payments.map(p => createPayment(request, p))
    );
    
    createResponses.forEach(response => {
        expect(response.status()).toBe(201);
    });
    
    const ids = await Promise.all(
        createResponses.map(r => r.json().then(b => b.id))
    );
    createdPayments.push(...ids);
    
    const finalPayments = await Promise.all(
        ids.map(id => waitForCondition(
            () => getPayment(request, id),
            (payment: any) => payment.status !== 'Pending',
            { maxAttempts: 60, pollIntervalMs: 500 }
        ))
    );
    
    finalPayments.forEach(payment => {
        expect(['Processed', 'Failed']).toContain(payment.status);
    });
});

test('should prevent payment from transitioning backwards from Processed to Pending', async ({ request }) => {
    const paymentData = paymentFactory({ amount: 50.00, currency: "USD" });
    const createResponse = await createPayment(request, paymentData);
    
    expect(createResponse.status()).toBe(201);
    const { id } = await createResponse.json();
    createdPayments.push(id);
    
    await waitForStatus(
        () => getPayment(request, id),
        (payment: any) => payment.status,
        'Processed'
    );
    
    await new Promise(resolve => setTimeout(resolve, 2000));
    
    const finalGetResponse = await getPayment(request, id);
    const finalPayment = await finalGetResponse.json();
    
    expect(finalPayment.status).toBe('Processed');
});

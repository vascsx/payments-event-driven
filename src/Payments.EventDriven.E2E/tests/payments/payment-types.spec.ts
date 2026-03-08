import { expect } from '@playwright/test';
import { test } from '../fixtures/payment-fixture';
import { paymentFactory } from '../factories/payment-factory';
import { createPayment, getPayment, createAndWaitProcessing } from '../helpers/payment-helper';
import { waitForStatus } from '../utils/polling';
import { createdPayments } from '../utils/cleanup';


test.describe('DARF payments (type 1)', () => {
    test('should create a DARF payment and persist the type correctly', async ({ request }) => {
        const paymentData = paymentFactory({
            amount: 150.00,
            currency: "BRL",
            type: 1
        });
        const response = await createPayment(request, paymentData);

        expect(response.status()).toBe(201);

        const body = await response.json();
        expect(body.id).toBeDefined();

        createdPayments.push(body.id);

        const payment = await getPayment(request, body.id);
        const paymentDetails = await payment.json();
        expect(paymentDetails.type).toBe(1);
    });

    test('should process DARF payment end-to-end using DarfPaymentHandler', async ({ request }) => {
        const paymentData = paymentFactory({
            amount: 300.00,
            currency: "BRL",
            type: 1
        });

        const { payment } = await createAndWaitProcessing(request, paymentData);

        expect(payment.status).toBe('Processed');
    });
});

test.describe('DARJ payments (type 2)', () => {
    test('should create a DARJ payment and persist the type correctly', async ({ request }) => {
        const paymentData = paymentFactory({
            amount: 200.00,
            currency: "BRL",
            type: 2
        });
        const response = await createPayment(request, paymentData);

        expect(response.status()).toBe(201);

        const body = await response.json();
        expect(body.id).toBeDefined();

        createdPayments.push(body.id);

        const payment = await getPayment(request, body.id);
        const paymentDetails = await payment.json();
        expect(paymentDetails.type).toBe(2);
    });

    test('should process DARJ payment end-to-end using DarjPaymentHandler', async ({ request }) => {
        const paymentData = paymentFactory({
            amount: 400.00,
            currency: "BRL",
            type: 2
        });

        const { payment } = await createAndWaitProcessing(request, paymentData);

        expect(payment.status).toBe('Processed');
    });
});

test('should route each payment type to its corresponding handler and preserve type after processing', async ({ request }) => {
    const defaultPayment = paymentFactory({ amount: 50.00, currency: "USD", type: 0 });
    const darfPayment = paymentFactory({ amount: 50.00, currency: "BRL", type: 1 });
    const darjPayment = paymentFactory({ amount: 50.00, currency: "BRL", type: 2 });

    const responses = await Promise.all([
        createPayment(request, defaultPayment),
        createPayment(request, darfPayment),
        createPayment(request, darjPayment)
    ]);

    responses.forEach(response => {
        expect(response.status()).toBe(201);
    });

    const ids = await Promise.all(responses.map(r => r.json().then(b => b.id)));
    createdPayments.push(...ids);

    const processedPayments = await Promise.all(
        ids.map(id => waitForStatus(
            () => getPayment(request, id),
            (payment: any) => payment.status,
            'Processed'
        ))
    );

    processedPayments.forEach(payment => {
        expect(payment.status).toBe('Processed');
    });

    test('should route each payment type to its corresponding handler and preserve type after processing', async ({ request }) => {
        const defaultPayment = paymentFactory({ amount: 50.00, currency: "USD", type: 0 });
        const darfPayment = paymentFactory({ amount: 50.00, currency: "BRL", type: 1 });
        const darjPayment = paymentFactory({ amount: 50.00, currency: "BRL", type: 2 });

        const responses = await Promise.all([
            createPayment(request, defaultPayment),
            createPayment(request, darfPayment),
            createPayment(request, darjPayment)
        ]);

        responses.forEach(response => {
            expect(response.status()).toBe(201);
        });

        const ids = await Promise.all(responses.map(r => r.json().then(b => b.id)));
        createdPayments.push(...ids);

        const processedPayments = await Promise.all(
            ids.map(id => waitForStatus(
                () => getPayment(request, id),
                (payment: any) => payment.status,
                'Processed'
            ))
        );

        processedPayments.forEach(payment => {
            expect(payment.status).toBe('Processed');
        });
    });
})
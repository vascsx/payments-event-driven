import { expect } from '@playwright/test';
import { test } from '../fixtures/payment-fixture';
import { paymentFactory } from '../factories/payment-factory';
import { createPayment, getPayment, deletePayment } from '../helpers/payment-helper';

test('should delete payment and return 204 when payment exists', async ({ request }) => {
    const paymentData = paymentFactory({ amount: 100.00, currency: "USD" });
    const createResponse = await createPayment(request, paymentData);
    
    expect(createResponse.status()).toBe(201);
    const { id } = await createResponse.json();
    
    const deleteResponse = await deletePayment(request, id);
    
    expect(deleteResponse.status()).toBe(204);
});

test('should return 404 when attempting to delete non-existent payment', async ({ request }) => {
    const nonExistentId = '00000000-0000-0000-0000-000000000000';
    const deleteResponse = await deletePayment(request, nonExistentId);
    
    expect(deleteResponse.status()).toBe(404);
});

test('should return 400 or 404 when payment ID format is invalid for deletion', async ({ request }) => {
    const invalidId = 'not-a-valid-guid';
    const deleteResponse = await request.delete(`/api/payments/${invalidId}`);
    
    expect([400, 404]).toContain(deleteResponse.status());
});

test('should make payment inaccessible via GET after successful deletion', async ({ request }) => {
    const paymentData = paymentFactory({ amount: 75.00, currency: "EUR" });
    const createResponse = await createPayment(request, paymentData);
    
    expect(createResponse.status()).toBe(201);
    const { id } = await createResponse.json();
    
    const deleteResponse = await deletePayment(request, id);
    expect(deleteResponse.status()).toBe(204);
    
    const getAfterDelete = await getPayment(request, id);
    expect(getAfterDelete.status()).toBe(404);
});

test('should return 404 when attempting to delete already deleted payment', async ({ request }) => {
    const paymentData = paymentFactory({ amount: 50.00, currency: "BRL" });
    const createResponse = await createPayment(request, paymentData);
    
    expect(createResponse.status()).toBe(201);
    const { id } = await createResponse.json();
    
    await deletePayment(request, id);
    
    const secondDeleteResponse = await deletePayment(request, id);
    expect(secondDeleteResponse.status()).toBe(404);
});

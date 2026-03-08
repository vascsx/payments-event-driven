import { APIRequestContext, expect } from '@playwright/test';
import { waitForStatus } from '../utils/polling';
import { createdPayments } from '../utils/cleanup';

export async function createPayment(request: APIRequestContext, data: { amount: number; currency: string }) {
    const response = await request.post('/api/payments', { data });
    return response;
}

export async function getPayment(request: APIRequestContext, id: string) {
    return request.get(`/api/payments/${id}`);
}

export async function deletePayment(request: APIRequestContext, id: string) {
    return request.delete(`/api/payments/${id}`);
}

export async function createAndWaitProcessing(request: APIRequestContext, data: any) {
    const response = await createPayment(request, data);
    expect(response.status()).toBe(201);
    
    const { id } = await response.json();
    createdPayments.push(id);
    
    const payment = await waitForStatus(
        () => getPayment(request, id),
        (p: any) => p.status,
        'Processed'
    );
    
    return { id, payment };
}


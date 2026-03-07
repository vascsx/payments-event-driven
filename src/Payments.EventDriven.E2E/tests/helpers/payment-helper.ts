import { APIRequestContext, expect } from '@playwright/test';

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


import { expect } from '@playwright/test';

export async function waitForStatus<T>(
    fetchFn: () => Promise<any>,
    statusExtractor: (data: T) => string,
    expectedStatus: string,
    options: {
        maxAttempts?: number;
        pollIntervalMs?: number;
    } = {}
): Promise<T> {
    const { maxAttempts = 30, pollIntervalMs = 500 } = options;
    let currentStatus = '';

    for (let attempt = 0; attempt < maxAttempts; attempt++) {
        const response = await fetchFn();
        expect(response.status()).toBe(200);

        const data: T = await response.json();
        currentStatus = statusExtractor(data);

        if (currentStatus === expectedStatus) {
            return data;
        }

        await new Promise(resolve => setTimeout(resolve, pollIntervalMs));
    }

    throw new Error(`Resource did not reach status '${expectedStatus}' after ${maxAttempts} attempts. Current status: ${currentStatus}`);
}
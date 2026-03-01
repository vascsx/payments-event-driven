import { test, expect } from '@playwright/test';

test('CT01 - Criação de Pagamento Válido', async ({ request }) => {
    const response = await request.post('/api/payments', {
        data: {
            amount: 10.00,
            currency: "USD"
        }
    });
    expect(response.status()).toBe(201);
    expect(await response.json()).toMatchObject({
        id: expect.any(String)
    });
});

test('CT02 - Rejeição de Pagamento com Valor Inválido', async ({ request }) => {
    const response = await request.post('/api/payments', {
        data: {
            amount: -10.00,
            currency: "USD"
        }
    });
    console.log(await response.json());
    expect(response.status()).toBe(400);
    expect((await response.json()).errors.Amount[0]).toBe("Amount must be greater than 0.");
});

test('CT03 - Rejeição de Pagamento com Moeda Ausente', async ({ request }) => {
    const response = await request.post('/api/payments', {
        data: {
            amount: 10.00,
            currency: ""
        }
    });
    console.log(await response.json());
    expect(response.status()).toBe(400);
    expect((await response.json()).errors.Currency[0]).toBe("The Currency field is required.");
});

test('CT04 - Rejeição de Pagamento com Precisão Monetária Inválida', async ({ request }) => {
    const response = await request.post('/api/payments', {
        data: {
            amount: 10.123,
            currency: "BRL"
        }
    });
    expect(response.status()).toBe(400);
    expect((await response.json()).detail).toBe("Amount cannot have more than 2 decimal places.");
});

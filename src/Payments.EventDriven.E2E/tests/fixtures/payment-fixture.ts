import { test as base } from '@playwright/test';
import { createdPayments } from '../utils/cleanup';
import { deletePayment } from '../helpers/payment-helper';

export const test = base.extend({});

test.afterEach(async ({ request }) => {

    for (const id of createdPayments) {
        await deletePayment(request, id);
    }

    createdPayments.length = 0;
});
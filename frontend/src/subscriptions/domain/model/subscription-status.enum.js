/**
 * Subscription lifecycle statuses — mirrors backend enum (case-insensitive compare).
 */

export const SubscriptionStatus = Object.freeze({
    ACTIVE: 'active',
    CANCELLED: 'cancelled',
    EXPIRED: 'expired',
    PENDING: 'pending',
});

/**
 * Returns true if status equals active (or cancelled but still within billing cycle).
 * @param {string|null|undefined} status
 * @param {string|Date|null|undefined} endDate
 */
export function isActiveStatus(status, endDate = null) {
    const s = String(status ?? '').trim().toLowerCase();
    if (s === SubscriptionStatus.ACTIVE) return true;
    if (s === SubscriptionStatus.CANCELLED && endDate) {
        const end = new Date(endDate);
        return !isNaN(end.getTime()) && end.getTime() > Date.now();
    }
    return false;
}

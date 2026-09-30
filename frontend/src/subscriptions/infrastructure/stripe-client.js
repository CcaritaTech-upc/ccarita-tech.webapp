let pending;

// Pure entry point has no script-loading side effect; URL checkout needs no SDK.
export function getStripeClient() {
  if (!pending) {
    pending = import('@stripe/stripe-js/pure')
      .then(({ loadStripe }) => loadStripe(import.meta.env.VITE_STRIPE_PUBLISHABLE_KEY))
      .catch(error => {
        pending = undefined;
        throw error;
      });
  }
  return pending;
}

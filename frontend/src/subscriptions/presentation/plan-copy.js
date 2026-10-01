const seededFeatureKeys = {
  starter: {
    'Up to 50 IoT devices': 'devices',
    'Basic dashboard': 'dashboard',
    'Email support': 'support',
    'Updates included': 'updates',
    '1 administrator': 'administrators',
    'Monthly reports': 'reports',
  },
  professional: {
    'Up to 200 IoT devices': 'devices',
    'Advanced dashboard': 'dashboard',
    '24/7 priority support': 'support',
    'Updates and new features': 'updates',
    '3 administrators': 'administrators',
    'Real-time reports': 'reports',
    'Custom API': 'api',
    'Training included': 'training',
  },
  enterprise: {
    'Unlimited IoT devices': 'devices',
    'Enterprise dashboard': 'dashboard',
    'Dedicated 24/7 support': 'support',
    'Development of custom features': 'customDevelopment',
    'Unlimited administrators': 'administrators',
    'Advanced analytics': 'analytics',
    'Complete API': 'api',
    'Specialized consulting': 'consulting',
    'Guaranteed SLA': 'sla',
  },
};

function planSlug(planName) {
  return String(planName ?? '').trim().toLowerCase();
}

export function translatePlanDescription(plan, t, te) {
  const key = `subscriptions.planCatalog.${planSlug(plan?.name)}.description`;
  return te(key) ? t(key) : (plan?.description ?? '');
}

export function translatePlanFeature(planName, feature, t, te) {
  const slug = planSlug(planName);
  const featureName = seededFeatureKeys[slug]?.[feature];
  if (!featureName) return feature;
  const key = `subscriptions.planCatalog.${slug}.features.${featureName}`;
  return te(key) ? t(key) : feature;
}

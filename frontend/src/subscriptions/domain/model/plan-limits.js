/**
 * Plan quota limits definitions and helpers
 */
export function getPlanLimits(plan) {
  const name = String(plan?.name || '').toLowerCase();
  const maxDevFromEntity = Number(plan?.maxDevices);

  if (name.includes('starter')) {
    return {
      name: 'Starter',
      maxDevices: maxDevFromEntity > 0 ? maxDevFromEntity : 50,
      maxProjects: 5,
      label: String(maxDevFromEntity > 0 ? maxDevFromEntity : 50),
      isUnlimited: false
    };
  }

  if (name.includes('pro')) {
    return {
      name: 'Professional',
      maxDevices: maxDevFromEntity > 0 ? maxDevFromEntity : 200,
      maxProjects: 15,
      label: String(maxDevFromEntity > 0 ? maxDevFromEntity : 200),
      isUnlimited: false
    };
  }

  if (name.includes('enterprise')) {
    return {
      name: 'Enterprise',
      maxDevices: Infinity,
      maxProjects: Infinity,
      label: 'Unlimited',
      isUnlimited: true
    };
  }

  if (maxDevFromEntity > 0) {
    return {
      name: plan?.name || 'Custom',
      maxDevices: maxDevFromEntity,
      maxProjects: 15,
      label: String(maxDevFromEntity),
      isUnlimited: false
    };
  }

  // Default to Pro if undetermined
  return {
    name: 'Professional',
    maxDevices: 200,
    maxProjects: 15,
    label: '200',
    isUnlimited: false
  };
}

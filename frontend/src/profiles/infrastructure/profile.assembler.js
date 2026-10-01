import { Profile } from '../domain/model/profile.entity.js';
import { IamFacade } from './iam.facade.js';

export class ProfileAssembler {
  static toDomainFromResponse(response) {
    if (!response) {
      return { profileEntity: new Profile({}), configData: {} };
    }

    // Get email and role from IAM bounded context through ACL (Anti-Corruption Layer)
    // These fields belong to IAM, not to profiles
    const emailFromIam = IamFacade.getCurrentUserEmail();
    const roleFromIam = IamFacade.getCurrentUserRole();

    const profileData = {
      id: response.id,
      userId: response.userId || 0,
      // Backend returns 'name' in the profile response
      name: response.name || response.fullName || '',
      // Email comes from IAM bounded context, not from profiles
      email: emailFromIam || '',
      secondEmail: response.secondEmail || response.alternateEmail || '',
      username: response.username || '',
      // Role comes from IAM bounded context, not from profiles
      role: roleFromIam || 'builder',
      age: response.age ?? 0,
      yearsInBusiness: response.yearsInBusiness ?? null,

      photoUrl: response.photoUrl && response.photoUrl !== 'undefined'
        ? response.photoUrl
        : (response.cloudinaryReference || undefined),

      phoneNumber: response.phoneNumber || response.phone || '',
      address: response.address || ''
    };

    return {
      profileEntity: new Profile(profileData),
      configData: {}
    };
  }
}

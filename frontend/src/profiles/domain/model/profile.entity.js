export class Profile {
  constructor({
    id = 0,
    userId = 0,
    name = '',
    email = '',
    username = '',
    address = '',
    age = 0,
    yearsInBusiness = null,
    phoneNumber = '',
    secondEmail = '',
    photoUrl = '',
    role = ''
  } = {}) {
    this.id = id;
    this.userId = userId;
    this.name = name;
    this.email = email;
    this.username = username;
    this.address = address;
    this.age = age;
    this.yearsInBusiness = yearsInBusiness;
    this.phoneNumber = phoneNumber;
    this.secondEmail = secondEmail;
    this.photoUrl = photoUrl;
    this.role = role;
  }

  get displayPhotoUrl() {
    return this.photoUrl && this.photoUrl.trim() !== ''
      ? this.photoUrl
      : 'data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 width=%2240%22 height=%2240%22%3E%3Crect width=%2240%22 height=%2240%22 rx=%224%22 fill=%22%2310B981%22/%3E%3Ctext x=%2220%22 y=%2226%22 text-anchor=%22middle%22 fill=%22white%22 font-size=%2218%22 font-family=%22sans-serif%22%3EU%3C/text%3E%3C/svg%3E';
  }
}

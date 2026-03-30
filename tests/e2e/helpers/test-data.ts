export const testUsers = {
  clubOwner: {
    email: 'club-owner@test.com',
    password: 'TestPassword123!',
    fullName: 'Club Owner',
    clubName: 'Elite Gymnastics Academy',
  },
  clubMember: {
    email: 'club-member@test.com',
    password: 'TestPassword123!',
    fullName: 'Club Member',
  },
  individual: {
    email: 'individual@test.com',
    password: 'TestPassword123!',
    fullName: 'Individual User',
  },
};

export function generateUniqueEmail(prefix: string): string {
  const timestamp = Date.now();
  return `${prefix}-${timestamp}@test.com`;
}

export function generateClubName(): string {
  const timestamp = Date.now();
  return `Test Club ${timestamp}`;
}

// Test fixtures — small TypeScript files for unit testing the extractors
export const SIMPLE_CLASS = `
export class UserService {
  private name: string;

  constructor(name: string) {
    this.name = name;
  }

  getName(): string {
    return this.name;
  }

  setName(name: string): void {
    this.name = name;
  }
}

export interface IUserService {
  getName(): string;
  setName(name: string): void;
}

export type UserId = string;

export enum UserRole {
  Admin = 'admin',
  User = 'user',
  Guest = 'guest',
}
`;

export const ARROW_FUNCTIONS = `
export const greet = (name: string): string => {
  return \`Hello, \${name}\`;
};

export const add = (a: number, b: number) => a + b;

// PascalCase => should be classified as Component
export const ProfileCard = (props: { name: string }) => {
  return props.name;
};
`;

export const BARREL_FILE = `
export { UserService } from './user-service';
export { ProfileCard } from './components/profile';
export * from './types';
`;

export const IMPORTS_FILE = `
import { UserService } from './user-service';
import type { UserId } from './types';
import * as utils from './utils';

export function createUser(id: UserId): UserService {
  return new UserService(id);
}
`;

export const MOCK_PACKAGE_JSON = JSON.stringify({
  name: 'test-monorepo',
  workspaces: ['apps/*', 'packages/*'],
});

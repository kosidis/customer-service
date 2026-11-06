import type { Config } from "jest";
import nextJest from "next/jest.js";

const createJestConfig = nextJest({ dir: "./" });

const config: Config = {
  testEnvironment: "jsdom",
  testMatch: ["**/*.test.{ts,tsx}"],
  setupFilesAfterEnv: ["<rootDir>/jest.setup.ts"],
  moduleNameMapper: {
    "^server-only$": "<rootDir>/test/empty-module.ts",
    "^@/(.*)$": "<rootDir>/src/$1",
  },
  collectCoverageFrom: [
    "src/**/*.{ts,tsx}",
    // Pure boilerplate: root layout/metadata and the static loading skeleton.
    "!src/app/layout.tsx",
    "!src/app/customers/loading.tsx",
    "!src/**/*.test.{ts,tsx}",
    "!src/__tests__/**",
    "!src/**/*.d.ts",
  ],
  coverageThreshold: {
    global: { lines: 80, branches: 80, functions: 80, statements: 80 },
  },
};

export default createJestConfig(config);

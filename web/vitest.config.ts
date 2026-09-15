import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  resolve: { tsconfigPaths: true },
  test: {
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
    pool: "threads",
    fileParallelism: false,
    restoreMocks: true,
    clearMocks: true,
    coverage: { reporter: ["text", "html"] },
  },
});

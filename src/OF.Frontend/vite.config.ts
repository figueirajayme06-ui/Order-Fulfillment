import { readFileSync, existsSync } from "node:fs";
import { resolve } from "node:path";
import reactPlugin from "@vitejs/plugin-react";
import { defineConfig } from "vite";
import checker from "vite-plugin-checker";

export default defineConfig({
  plugins: [
    reactPlugin(),
    checker({
      typescript: { tsconfigPath: "./tsconfig.json" },
      overlay: { initialIsOpen: true },
    }),
  ],
  resolve: {
    alias: {
      "@": resolve(__dirname, "src"),
    },
  },
  server: {
    port: 5174,
    proxy: {
      "/api": {
        target: "https://localhost:7200",
        secure: false,
        headers: { Connection: "Keep-Alive" },
      },
    },
    https: getHttpsConfig(),
  },
  build: {
    sourcemap: true,
    minify: true,
    cssMinify: true,
    outDir: "dist",
  },
  test: {
    include: ["./src/**/*.test.ts?(x)"],
    environment: "jsdom",
    setupFiles: ["./src/test-setup.ts"],
  },
});

function getHttpsConfig() {
  const keyPath = resolve(__dirname, "certs", "localhost-key.pem");
  const certPath = resolve(__dirname, "certs", "localhost.pem");

  if (existsSync(keyPath) && existsSync(certPath)) {
    return {
      key: readFileSync(keyPath),
      cert: readFileSync(certPath),
    };
  }

  return undefined;
}

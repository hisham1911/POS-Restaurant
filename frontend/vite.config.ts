import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "path";

export default defineConfig(({ mode }) => ({
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  build: {
    outDir: "dist",
    sourcemap: mode === "development",
    minify: "terser",
    terserOptions: {
      compress: {
        drop_console: mode === "production",
        drop_debugger: true,
      },
    },
    rollupOptions: {
      output: {
        manualChunks: {
          vendor: ["react", "react-dom", "react-router-dom"],
          redux: ["@reduxjs/toolkit", "react-redux", "redux-persist"],
          ui: ["@headlessui/react", "@heroicons/react", "lucide-react"],
        },
        // Ensure font files are properly copied with correct paths
        assetFileNames: (assetInfo) => {
          if (assetInfo.name?.endsWith('.woff') || assetInfo.name?.endsWith('.woff2') || assetInfo.name?.endsWith('.ttf')) {
            return 'assets/[name]-[hash][extname]';
          }
          return 'assets/[name]-[hash][extname]';
        },
      },
    },
    chunkSizeWarningLimit: 1000,
    // Windows 7 compatibility: ensure proper asset handling
    assetsInlineLimit: 0, // Don't inline fonts, keep them as separate files
  },
  server: {
    port: 3000,
    proxy: {
      "/api": {
        target: "http://localhost:5243",
        changeOrigin: true,
      },
    },
  },
  // Optimize font loading for Windows 7
  optimizeDeps: {
    include: ['@fontsource/cairo'],
  },
}));

import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import { quasar, transformAssetUrls } from "@quasar/vite-plugin";
import path from "path";

export default defineConfig({
  base: "/",
  plugins: [
    vue({
      template: { transformAssetUrls }
    }),
    quasar({
      sassVariables: "src/style/quasar-variables.sass"
    })
  ],
  build: {
    sourcemap: false,
    target: "esnext"
  },
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "src")
    }
  }
});

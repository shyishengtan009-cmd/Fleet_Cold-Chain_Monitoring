import "@quasar/extras/fontawesome-v6/fontawesome-v6.css";
import "quasar/src/css/index.sass";
// Generates the responsive breakpoint-specific spacing classes (q-pt-md-lg, q-px-xs-md, etc.)
// that App.vue relies on to cancel .header-app's `margin: -24px`. NOT included in index.sass —
// missing this import was mistaken for those classes being fake/nonexistent in an earlier pass.
import "quasar/src/css/flex-addon.sass";
import { createPinia } from "pinia";
import { Dialog, Loading, Notify, Quasar } from "quasar";
import iconSet from "quasar/icon-set/fontawesome-v6";
import { createApp } from "vue";
import App from "./App.vue";
import router from "./router/router";
import api from "./helpers/api";
import { TOKEN_KEY } from "./utils/constants";
import "./style/global.sass";
import "./style/style.sass";
import "./style/scada.css";

async function bootstrap() {
  // Stand-in for the real HIAS login (which doesn't exist in this standalone demo) —
  // see Backend/HIAS-NET-CORE/Controllers/AuthDemoController.cs. Every Fleet endpoint
  // reads an OrganizationId/RoleCode claim from this token, so the app can't do
  // anything useful until this resolves.
  try {
    const { token } = await api.auth.demoLogin();
    localStorage.setItem(TOKEN_KEY, token);
  } catch (err) {
    console.error("Demo login failed — is the backend running on the configured baseURL?", err);
  }

  const app = createApp(App);
  app.use(createPinia());
  app.use(router);
  app.use(Quasar, {
    iconSet,
    plugins: { Dialog, Notify, Loading }
  });
  app.mount("#app");
}

bootstrap();

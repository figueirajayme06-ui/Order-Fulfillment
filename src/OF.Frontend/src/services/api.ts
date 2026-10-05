import axios from "axios";

const api = axios.create({
  // No baseURL — Vite proxy handles /api requests in dev,
  // and in production the SPA is served from the same origin.
});

api.interceptors.response.use(
  (response) => response,
  (error) => {
    const requestPath = typeof error.config?.url === "string" ? error.config.url.split("?", 1)[0] : "";
    const isCurrentUserProbe = requestPath.endsWith("/api/auth/me");

    if (error.response?.status === 401 && !isCurrentUserProbe) {
      // EasyAuth will handle the redirect — reload to trigger it
      window.location.reload();
    }
    return Promise.reject(error);
  },
);

export default api;

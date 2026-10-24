import { reactRouter } from '@react-router/dev/vite'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const apiTarget = process.env.API_BASE_URL || process.env.VITE_API_URL || env.API_BASE_URL || env.VITE_API_URL || 'http://127.0.0.1:56193'

  return {
    plugins: [reactRouter()],
    server: {
      port: 5173,
      proxy: {
        '/api': {
          target: apiTarget,
          changeOrigin: true,
          secure: false,
          configure: (proxy) => {
            proxy.on('error', (_err, _req, res) => {
              if (res && res.writeHead && !res.headersSent) {
                res.writeHead(503, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ error: `NewsApi is offline or starting up at ${apiTarget}` }));
              }
            });
          }
        }
      }
    }
  }
})


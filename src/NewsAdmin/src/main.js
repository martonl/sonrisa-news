import { createApp } from 'vue';
import App from './App.vue';
import router from './router';
import './style.css';
import { authStore } from './stores/authStore';

async function bootstrap() {
	await authStore.hydrate();
	createApp(App).use(router).mount('#app');
}

void bootstrap();
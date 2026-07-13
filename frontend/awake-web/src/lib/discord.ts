const API_URL = import.meta.env.VITE_API_URL ?? ''

// Единая точка входа в Discord OAuth (лендинг, hero, логин)
export const discordLoginUrl = `${API_URL}/api/auth/discord/login`

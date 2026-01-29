window.auth = {
    login: async (username, password) => {
        try {
            const response = await fetch('/api/auth/login', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ username, password })
            });

            if (response.ok) {
                const data = await response.json();
                return { success: true, data: data };
            } else {
                // Try to parse error message from response
                let errorMessage = response.statusText;
                try {
                    const errorData = await response.json();
                    if (errorData && errorData.error) {
                        errorMessage = errorData.error;
                    }
                } catch (e) {
                    // Ignore JSON parse error, use statusText
                }
                return { success: false, error: errorMessage };
            }
        } catch (error) {
            return { success: false, error: error.message };
        }
    },
    logout: async () => {
        try {
            await fetch('/api/auth/logout', { method: 'POST' });
            return true;
        } catch (e) {
            return false;
        }
    }
};
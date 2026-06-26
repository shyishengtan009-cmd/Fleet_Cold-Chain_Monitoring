export const getConfig = async () => {
  const response = await fetch("/appsettings.json");
  if (!response.ok) {
    throw new Error("Failed to load configuration");
  }
  return await response.json();
};

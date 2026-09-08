export function getRoleFromToken(token: string): string {
  try {
    const payloadBase64 = token.split('.')[1];
    const decodedPayload = JSON.parse(atob(payloadBase64));
    return decodedPayload['role']
      || decodedPayload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
      || '';
  } catch {
    return '';
  }
}

export function isAdminUser(): boolean {
  const storedUser = localStorage.getItem('cosmicStockUser');
  if (!storedUser) return false;
  const user = JSON.parse(storedUser);
  return getRoleFromToken(user.token) === 'Admin';
}

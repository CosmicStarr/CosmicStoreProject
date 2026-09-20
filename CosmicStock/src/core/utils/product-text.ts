export function excerptProductDescription(
  htmlOrText: string | null | undefined,
  maxLength = 220,
): string {
  if (!htmlOrText?.trim()) return '';

  const text = htmlOrText
    .replace(/<[^>]+>/g, ' ')
    .replace(/&nbsp;/gi, ' ')
    .replace(/&amp;/gi, '&')
    .replace(/&lt;/gi, '<')
    .replace(/&gt;/gi, '>')
    .replace(/&quot;/gi, '"')
    .replace(/\s+/g, ' ')
    .trim();

  if (!text) return '';
  if (text.length <= maxLength) return text;

  const cut = text.lastIndexOf(' ', maxLength);
  const end = cut < maxLength / 2 ? maxLength : cut;
  return `${text.slice(0, end).trimEnd()}…`;
}

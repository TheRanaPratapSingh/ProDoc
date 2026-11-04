export function pickFile(accept: string): Promise<File | null> {
  const documentRef = (globalThis as { document?: Document }).document;
  if (!documentRef) {
    return Promise.resolve(null);
  }

  return new Promise((resolve) => {
    const input = documentRef.createElement('input');
    input.type = 'file';
    input.accept = accept;
    input.onchange = () => resolve(input.files?.[0] ?? null);
    input.click();
  });
}

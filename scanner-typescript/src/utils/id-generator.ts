import crypto from 'node:crypto';

/**
 * Generates a deterministic atom ID from namespace + name.
 * Matches the ID format used by the C# CSharpScanner.
 */
export function generateAtomId(
  namespace: string,
  name: string,
  type: string
): string {
  const raw = `${namespace}.${name}`.toLowerCase();
  return `${type.toLowerCase()}:${raw}`;
}

/**
 * Generates a deterministic link ID from source + target + type.
 */
export function generateLinkId(
  sourceId: string,
  targetId: string,
  type: string
): string {
  const hash = crypto
    .createHash('md5')
    .update(`${sourceId}|${targetId}|${type}`)
    .digest('hex')
    .substring(0, 8);
  return `link:${hash}`;
}

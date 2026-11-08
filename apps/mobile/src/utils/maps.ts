import { Linking, Platform } from 'react-native';

export type MapLocation = {
  name?: string | null;
  address?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  googlePlaceId?: string | null;
  mapsUrl?: string | null;
  directionsUrl?: string | null;
};

function encode(value: string) {
  return encodeURIComponent(value);
}

function queryFrom(location: MapLocation): string | null {
  if (location.latitude != null && location.longitude != null) {
    return `${location.latitude},${location.longitude}`;
  }

  const parts = [location.name, location.address].map((part) => part?.trim()).filter(Boolean);
  if (parts.length === 0) {
    return null;
  }

  return parts.join(', ');
}

/** Google Maps search URL for a specific clinic / practice. */
export function buildMapsSearchUrl(location: MapLocation): string | null {
  if (location.mapsUrl?.trim()) {
    return location.mapsUrl.trim();
  }

  const query = queryFrom(location);
  if (!query) {
    return null;
  }

  let url = `https://www.google.com/maps/search/?api=1&query=${encode(query)}`;
  if (location.googlePlaceId?.trim()) {
    url += `&query_place_id=${encode(location.googlePlaceId.trim())}`;
  }

  return url;
}

/** Google Maps directions URL to the clinic destination. */
export function buildDirectionsUrl(location: MapLocation): string | null {
  if (location.directionsUrl?.trim()) {
    return location.directionsUrl.trim();
  }

  let destination: string | null = null;
  if (location.latitude != null && location.longitude != null) {
    destination = `${location.latitude},${location.longitude}`;
  } else {
    destination = queryFrom(location);
  }

  if (!destination) {
    return null;
  }

  let url = `https://www.google.com/maps/dir/?api=1&destination=${encode(destination)}`;
  if (location.googlePlaceId?.trim()) {
    url += `&destination_place_id=${encode(location.googlePlaceId.trim())}`;
  }

  return url;
}

export function hasMapDestination(location: MapLocation): boolean {
  return Boolean(buildMapsSearchUrl(location));
}

export async function openMapsLocation(location: MapLocation): Promise<boolean> {
  const url = buildMapsSearchUrl(location);
  if (!url) {
    return false;
  }

  try {
    await Linking.openURL(url);
    return true;
  } catch {
    if (Platform.OS === 'web' && typeof window !== 'undefined') {
      window.open(url, '_blank', 'noopener,noreferrer');
      return true;
    }
    return false;
  }
}

export async function openDirections(location: MapLocation): Promise<boolean> {
  const url = buildDirectionsUrl(location);
  if (!url) {
    return false;
  }

  try {
    await Linking.openURL(url);
    return true;
  } catch {
    if (Platform.OS === 'web' && typeof window !== 'undefined') {
      window.open(url, '_blank', 'noopener,noreferrer');
      return true;
    }
    return false;
  }
}

export function formatClinicAddress(parts: {
  addressLine?: string | null;
  addressLine2?: string | null;
  landmark?: string | null;
  locality?: string | null;
  city?: string | null;
  district?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  formattedAddress?: string | null;
}): string {
  if (parts.formattedAddress?.trim()) {
    return parts.formattedAddress.trim();
  }

  const line = [
    parts.addressLine,
    parts.addressLine2,
    parts.landmark,
    parts.locality,
    parts.city,
    parts.district,
    parts.state,
  ]
    .map((item) => item?.trim())
    .filter(Boolean);

  let result = line.join(', ');
  if (parts.postalCode?.trim()) {
    result = result ? `${result} – ${parts.postalCode.trim()}` : parts.postalCode.trim();
  }

  if (parts.country?.trim() && parts.country.trim().toLowerCase() !== 'india') {
    result = result ? `${result}, ${parts.country.trim()}` : parts.country.trim();
  }

  return result;
}

export function formatCompactAddress(locality?: string | null, city?: string | null, postalCode?: string | null): string {
  return [locality, city, postalCode].map((part) => part?.trim()).filter(Boolean).join(', ');
}

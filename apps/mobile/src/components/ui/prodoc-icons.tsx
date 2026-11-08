import Svg, { Circle, Path, Rect } from 'react-native-svg';

/** Thin-line icons matching the patient home reference. */
export function IconSearch({ size = 20, color = '#64748B' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Circle cx="11" cy="11" r="6.5" stroke={color} strokeWidth={1.8} />
      <Path d="M16.5 16.5L20 20" stroke={color} strokeWidth={1.8} strokeLinecap="round" />
    </Svg>
  );
}

export function IconBell({ size = 22, color = '#14243D' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path
        d="M6 16V10a6 6 0 1112 0v6l1.2 1.8a1 1 0 01-.85 1.55H5.65a1 1 0 01-.85-1.55L6 16z"
        stroke={color}
        strokeWidth={1.7}
        strokeLinejoin="round"
      />
      <Path d="M10 19a2 2 0 004 0" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
    </Svg>
  );
}

export function IconCalendarPlus({ size = 24, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Rect x="3.5" y="5" width="17" height="15" rx="2.5" stroke={color} strokeWidth={1.7} />
      <Path d="M8 3.5V6.5M16 3.5V6.5M3.5 9.5h17" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
      <Path d="M12 12.5v5M9.5 15h5" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
    </Svg>
  );
}

export function IconOnlineConsult({ size = 24, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Rect x="2.5" y="5" width="14" height="11" rx="2" stroke={color} strokeWidth={1.7} />
      <Path d="M16.5 10l4-2.5v8L16.5 13" stroke={color} strokeWidth={1.7} strokeLinejoin="round" />
      <Path d="M7 19h7" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
    </Svg>
  );
}

export function IconClinic({ size = 24, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path
        d="M4 20V8.5L8 5l4 3.5V20M12 20V9.5L16 6l4 3.5V20"
        stroke={color}
        strokeWidth={1.7}
        strokeLinejoin="round"
      />
      <Path d="M7.5 12h1M7.5 15h1M14.5 12h1M14.5 15h1" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
    </Svg>
  );
}

export function IconHealthRecords({ size = 24, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path
        d="M8 4.5h6.5L18 8v11.5a2 2 0 01-2 2H8a2 2 0 01-2-2V6.5a2 2 0 012-2z"
        stroke={color}
        strokeWidth={1.7}
        strokeLinejoin="round"
      />
      <Path
        d="M12 11.2c-.8-1-2.2-.7-2.2.5 0 1.6 2.2 3 2.2 3s2.2-1.4 2.2-3c0-1.2-1.4-1.5-2.2-.5z"
        stroke={color}
        strokeWidth={1.5}
        strokeLinejoin="round"
      />
    </Svg>
  );
}

export function IconHome({ size = 22, color = '#356F5E', filled = false }: { size?: number; color?: string; filled?: boolean }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path
        d="M4 10.5L12 4l8 6.5V20a1.5 1.5 0 01-1.5 1.5H5.5A1.5 1.5 0 014 20V10.5z"
        stroke={color}
        strokeWidth={1.7}
        strokeLinejoin="round"
        fill={filled ? color : 'none'}
      />
      {!filled ? <Path d="M10 21.5V14h4v7.5" stroke={color} strokeWidth={1.7} strokeLinejoin="round" /> : null}
      {filled ? <Path d="M10 21.5V14h4v7.5" stroke="#FFFFFF" strokeWidth={1.7} strokeLinejoin="round" /> : null}
    </Svg>
  );
}

export function IconAppointments({ size = 22, color = '#64748B' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Rect x="3.5" y="5" width="17" height="15" rx="2.5" stroke={color} strokeWidth={1.7} />
      <Path d="M8 3.5V6.5M16 3.5V6.5M3.5 9.5h17" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
      <Path d="M8 14h3M13 14h3M8 17h3" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
    </Svg>
  );
}

export function IconRecords({ size = 22, color = '#64748B' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Rect x="4" y="5" width="16" height="14" rx="2.5" stroke={color} strokeWidth={1.7} />
      <Circle cx="9" cy="11" r="2" stroke={color} strokeWidth={1.7} />
      <Path d="M13 10h4M13 13h4M7 16h10" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
    </Svg>
  );
}

export function IconProfile({ size = 22, color = '#64748B' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Circle cx="12" cy="9" r="3.5" stroke={color} strokeWidth={1.7} />
      <Path d="M5.5 19c1.4-3 4-4.5 6.5-4.5s5.1 1.5 6.5 4.5" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
    </Svg>
  );
}

export function IconMoreGrid({ size = 22, color = '#64748B' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Rect x="4" y="4" width="6.5" height="6.5" rx="1.5" stroke={color} strokeWidth={1.7} />
      <Rect x="13.5" y="4" width="6.5" height="6.5" rx="1.5" stroke={color} strokeWidth={1.7} />
      <Rect x="4" y="13.5" width="6.5" height="6.5" rx="1.5" stroke={color} strokeWidth={1.7} />
      <Rect x="13.5" y="13.5" width="6.5" height="6.5" rx="1.5" stroke={color} strokeWidth={1.7} />
    </Svg>
  );
}

export function IconPeople({ size = 22, color = '#64748B' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Circle cx="9" cy="8" r="3" stroke={color} strokeWidth={1.7} />
      <Path d="M3.5 19c.9-3 2.8-4.5 5.5-4.5s4.6 1.5 5.5 4.5" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
      <Circle cx="17" cy="9" r="2.4" stroke={color} strokeWidth={1.6} />
      <Path d="M14.2 19c.5-1.8 1.6-2.9 3-3.3" stroke={color} strokeWidth={1.6} strokeLinecap="round" />
    </Svg>
  );
}

export function IconChevronRight({ size = 18, color = '#94A3B8' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path d="M9 6l6 6-6 6" stroke={color} strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round" />
    </Svg>
  );
}

export function IconStethoscope({ size = 20, color = '#FFFFFF' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path
        d="M6 4v6a4 4 0 008 0V4M6 4h2M14 4h2"
        stroke={color}
        strokeWidth={1.7}
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <Path d="M18 11v3.5a3.5 3.5 0 11-7 0V13" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
      <Circle cx="18" cy="8" r="2" stroke={color} strokeWidth={1.7} />
    </Svg>
  );
}

export function IconSpecialtyPhysician({ size = 26, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Circle cx="12" cy="7" r="3" stroke={color} strokeWidth={1.7} />
      <Path d="M6.5 20c1-4 3.2-6 5.5-6s4.5 2 5.5 6" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
      <Path d="M17 9.5v4M15 11.5h4" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
    </Svg>
  );
}

export function IconSpecialtyHeart({ size = 26, color = '#C45C5C' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path
        d="M12 19s-6.5-4.2-6.5-9A3.7 3.7 0 0112 7.8 3.7 3.7 0 0118.5 10c0 4.8-6.5 9-6.5 9z"
        stroke={color}
        strokeWidth={1.7}
        strokeLinejoin="round"
      />
      <Path d="M8.5 11.5h2l1.2-2 1.6 4 1.2-2H16" stroke={color} strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" />
    </Svg>
  );
}

export function IconSpecialtyDerm({ size = 26, color = '#B8753A' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path
        d="M12 4c-2.2 2.4-3.5 4.8-3.5 7.2A3.5 3.5 0 0012 14.7a3.5 3.5 0 003.5-3.5C15.5 8.8 14.2 6.4 12 4z"
        stroke={color}
        strokeWidth={1.7}
        strokeLinejoin="round"
      />
      <Path d="M10 17.5c.7 1.2 1.4 1.8 2 1.8s1.3-.6 2-1.8" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
    </Svg>
  );
}

export function IconSpecialtyPeds({ size = 26, color = '#A67C2D' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Circle cx="12" cy="9" r="3.2" stroke={color} strokeWidth={1.7} />
      <Path d="M8 20c.8-3 2.5-4.5 4-4.5s3.2 1.5 4 4.5" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
      <Path d="M8.5 7.2c-.8-.9-1.8-1.2-2.4-.6M15.5 7.2c.8-.9 1.8-1.2 2.4-.6" stroke={color} strokeWidth={1.5} strokeLinecap="round" />
    </Svg>
  );
}

/** Lucide-style arrow-left for navigation headers. */
export function IconBack({ size = 22, color = '#14243D' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path
        d="M19 12H5M12 19l-7-7 7-7"
        stroke={color}
        strokeWidth={1.8}
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </Svg>
  );
}

export function IconStar({ size = 14, color = '#E0A800' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill={color}>
      <Path d="M12 3.6l2.4 4.9 5.4.8-3.9 3.8.9 5.4L12 16.1 7.2 18.5l.9-5.4L4.2 9.3l5.4-.8L12 3.6z" />
    </Svg>
  );
}

export function IconHeart({
  size = 22,
  color = '#E25555',
  filled = false,
}: {
  size?: number;
  color?: string;
  filled?: boolean;
}) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill={filled ? color : 'none'}>
      <Path
        d="M12 20s-6.8-4.2-6.8-9.2A3.9 3.9 0 0112 8.2a3.9 3.9 0 016.8 2.6C18.8 15.8 12 20 12 20z"
        stroke={color}
        strokeWidth={1.7}
        strokeLinejoin="round"
      />
    </Svg>
  );
}

export function IconChevron({
  size = 18,
  color = '#64748B',
  direction = 'right',
}: {
  size?: number;
  color?: string;
  direction?: 'right' | 'down';
}) {
  const d = direction === 'down' ? 'M6 9l6 6 6-6' : 'M9 6l6 6-6 6';
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path d={d} stroke={color} strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round" />
    </Svg>
  );
}

export function IconEdit({ size = 18, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path
        d="M4 20h4.5L19 9.5 14.5 5 4 15.5V20z"
        stroke={color}
        strokeWidth={1.7}
        strokeLinejoin="round"
      />
      <Path d="M13.2 6.3l4.5 4.5" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
    </Svg>
  );
}

export function IconBellOutline({ size = 20, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path
        d="M6 16V10a6 6 0 1112 0v6l1.2 1.8a1 1 0 01-.85 1.55H5.65a1 1 0 01-.85-1.55L6 16z"
        stroke={color}
        strokeWidth={1.7}
        strokeLinejoin="round"
      />
      <Path d="M10 19a2 2 0 004 0" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
    </Svg>
  );
}

export function IconGlobe({ size = 20, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Circle cx="12" cy="12" r="8.5" stroke={color} strokeWidth={1.7} />
      <Path d="M3.5 12h17M12 3.5c2.5 2.8 3.8 5.6 3.8 8.5S14.5 17.7 12 20.5C9.5 17.7 8.2 14.9 8.2 12S9.5 6.3 12 3.5z" stroke={color} strokeWidth={1.7} />
    </Svg>
  );
}

export function IconMoon({ size = 20, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path
        d="M16.5 14.2A7 7 0 019 5.2 7.2 7.2 0 0016.5 14.2z"
        stroke={color}
        strokeWidth={1.7}
        strokeLinejoin="round"
      />
    </Svg>
  );
}

export function IconHelp({ size = 20, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Circle cx="12" cy="12" r="8.5" stroke={color} strokeWidth={1.7} />
      <Path d="M9.5 9.2a2.5 2.5 0 014.2 1.8c0 1.4-1.4 1.8-2.1 2.4" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
      <Circle cx="12" cy="16.5" r="1" fill={color} />
    </Svg>
  );
}

export function IconGift({ size = 20, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Rect x="4" y="11" width="16" height="9" rx="1.5" stroke={color} strokeWidth={1.7} />
      <Path d="M12 11v9M4 15h16M8.5 11c-1.5 0-2.5-1.2-2.5-2.5S7.5 6 9 7.2 12 11 12 11s1.5-2.6 3-3.8 2.5-.5 2.5 1.3S16.5 11 15 11H8.5z" stroke={color} strokeWidth={1.7} strokeLinejoin="round" />
    </Svg>
  );
}

export function IconBuilding({ size = 20, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path d="M4 20V7l8-3 8 3v13" stroke={color} strokeWidth={1.7} strokeLinejoin="round" />
      <Path d="M9 20v-5h6v5M9 10h1.5M13.5 10H15M9 13.5h1.5M13.5 13.5H15" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
    </Svg>
  );
}

export function IconSpark({ size = 20, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path d="M12 3l1.6 5.4L19 10l-5.4 1.6L12 17l-1.6-5.4L5 10l5.4-1.6L12 3z" stroke={color} strokeWidth={1.7} strokeLinejoin="round" />
      <Path d="M18 15l.7 2.3L21 18l-2.3.7L18 21l-.7-2.3L15 18l2.3-.7L18 15z" stroke={color} strokeWidth={1.5} strokeLinejoin="round" />
    </Svg>
  );
}

/** Compact healthcare assistant mark for the Vaidya FAB. */
export function IconVaidyaAssistant({ size = 28, color = '#FFFFFF' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Circle cx="12" cy="8" r="3.2" stroke={color} strokeWidth={1.7} />
      <Path
        d="M5.5 19.2c.8-3.2 3-5 6.5-5s5.7 1.8 6.5 5"
        stroke={color}
        strokeWidth={1.7}
        strokeLinecap="round"
      />
      <Path d="M17.2 6.2l1.1-1.1M18.8 8.2h1.4M17.2 10.2l1.1 1.1" stroke={color} strokeWidth={1.5} strokeLinecap="round" />
      <Path d="M12 12.8v2.4M10.8 14h2.4" stroke={color} strokeWidth={1.6} strokeLinecap="round" />
    </Svg>
  );
}

export function IconInfo({ size = 20, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Circle cx="12" cy="12" r="8.5" stroke={color} strokeWidth={1.7} />
      <Path d="M12 11v5.5M12 8.2h.01" stroke={color} strokeWidth={1.8} strokeLinecap="round" />
    </Svg>
  );
}

export function IconBriefcase({ size = 20, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Rect x="3.5" y="8" width="17" height="11" rx="2" stroke={color} strokeWidth={1.7} />
      <Path d="M9 8V6.5A1.5 1.5 0 0110.5 5h3A1.5 1.5 0 0115 6.5V8M3.5 13h17" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
    </Svg>
  );
}

export function IconSpecialtyNeuro({ size = 26, color = '#5B6BC0' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path
        d="M12 4c-3.2 0-5.5 2.2-5.5 5.2 0 2.2 1.2 3.6 2.4 4.6L12 20l3.1-6.2c1.2-1 2.4-2.4 2.4-4.6C17.5 6.2 15.2 4 12 4z"
        stroke={color}
        strokeWidth={1.7}
        strokeLinejoin="round"
      />
      <Path d="M9.5 9.5h.01M12 8.5h.01M14.5 9.5h.01" stroke={color} strokeWidth={2.2} strokeLinecap="round" />
    </Svg>
  );
}

export function IconSpecialtyOrtho({ size = 26, color = '#6B8F71' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path d="M8 5v4l-2 3v7M16 5v4l2 3v7M8 9h8" stroke={color} strokeWidth={1.7} strokeLinecap="round" strokeLinejoin="round" />
      <Circle cx="8" cy="4" r="1.5" stroke={color} strokeWidth={1.5} />
      <Circle cx="16" cy="4" r="1.5" stroke={color} strokeWidth={1.5} />
    </Svg>
  );
}

export function IconSpecialtyGyn({ size = 26, color = '#C06B8F' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Circle cx="12" cy="9" r="4" stroke={color} strokeWidth={1.7} />
      <Path d="M12 13v7M9.5 17.5h5" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
    </Svg>
  );
}

export function IconSpecialtyDental({ size = 26, color = '#4A90A4' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path
        d="M8 4.5c2-.8 6-.8 8 0 1.2.5 2 1.8 2 3.2 0 2.2-1 5-2.2 8.2-.5 1.3-1.6 2.1-2.8 1.6-.8-.3-1.2-1-1.5-1.8-.3.8-.7 1.5-1.5 1.8-1.2.5-2.3-.3-2.8-1.6C6 12.7 5 9.9 5 7.7c0-1.4.8-2.7 2-3.2z"
        stroke={color}
        strokeWidth={1.7}
        strokeLinejoin="round"
      />
    </Svg>
  );
}

export function IconSpecialtyEnt({ size = 26, color = '#8A6D3B' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path
        d="M8 10c0-2.8 2-5 4.5-5S17 7.2 17 10c0 2-1 3-2.2 3.8L14 16.5c-.3.6-.9 1-1.6 1H11"
        stroke={color}
        strokeWidth={1.7}
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <Circle cx="11" cy="19.5" r="1.2" fill={color} />
    </Svg>
  );
}

export function IconSpecialtyEye({ size = 26, color = '#3D7EA6' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path d="M2.5 12s3.5-6 9.5-6 9.5 6 9.5 6-3.5 6-9.5 6-9.5-6-9.5-6z" stroke={color} strokeWidth={1.7} strokeLinejoin="round" />
      <Circle cx="12" cy="12" r="2.5" stroke={color} strokeWidth={1.7} />
    </Svg>
  );
}

export function IconHealthDiabetes({ size = 24, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path d="M12 3.5c3.5 4.2 6 7.4 6 10.2A6 6 0 016 13.7C6 10.9 8.5 7.7 12 3.5z" stroke={color} strokeWidth={1.7} strokeLinejoin="round" />
      <Path d="M9.5 14.5h5" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
    </Svg>
  );
}

export function IconHealthBp({ size = 24, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Rect x="4" y="7" width="12" height="10" rx="2" stroke={color} strokeWidth={1.7} />
      <Path d="M16 10h2.5a1.5 1.5 0 010 3H16M8 11.5h4M8 14h2.5" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
    </Svg>
  );
}

export function IconHealthCholesterol({ size = 24, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Circle cx="12" cy="12" r="7.5" stroke={color} strokeWidth={1.7} />
      <Path d="M12 7.5v9M8.5 10.5c1.2-1 2.4-1.5 3.5-1.5s2.3.5 3.5 1.5" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
    </Svg>
  );
}

export function IconMentalHealth({ size = 24, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Path
        d="M8 18c-2.5 0-4.5-2.2-4.5-5.2C3.5 8.5 7 5 12 5s8.5 3.5 8.5 7.8c0 2.4-1.2 4.4-3.2 5.2L16 20l-2.2-2H8z"
        stroke={color}
        strokeWidth={1.7}
        strokeLinejoin="round"
      />
      <Path d="M9 11.5h.01M12 11.5h.01M15 11.5h.01" stroke={color} strokeWidth={2.2} strokeLinecap="round" />
    </Svg>
  );
}

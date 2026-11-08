import { type ReactNode } from 'react';
import Svg, { Circle, Path, Rect } from 'react-native-svg';

type IconProps = { size?: number; color?: string };

function base({ size = 18, color = '#64748B', children }: IconProps & { children: ReactNode }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      {children}
    </Svg>
  );
}

export function AdminIconOverview(props: IconProps) {
  return base({
    ...props,
    children: (
      <>
        <Rect x="3" y="3" width="8" height="8" rx="2" stroke={props.color ?? '#64748B'} strokeWidth={1.7} />
        <Rect x="13" y="3" width="8" height="5" rx="2" stroke={props.color ?? '#64748B'} strokeWidth={1.7} />
        <Rect x="13" y="10" width="8" height="11" rx="2" stroke={props.color ?? '#64748B'} strokeWidth={1.7} />
        <Rect x="3" y="13" width="8" height="8" rx="2" stroke={props.color ?? '#64748B'} strokeWidth={1.7} />
      </>
    ),
  });
}

export function AdminIconChart(props: IconProps) {
  return base({
    ...props,
    children: (
      <Path
        d="M4 19V5M4 19h16M8 16v-5M12 16V8M16 16v-3"
        stroke={props.color ?? '#64748B'}
        strokeWidth={1.7}
        strokeLinecap="round"
      />
    ),
  });
}

export function AdminIconUsers(props: IconProps) {
  return base({
    ...props,
    children: (
      <>
        <Circle cx="9" cy="8" r="3" stroke={props.color ?? '#64748B'} strokeWidth={1.7} />
        <Path d="M3.5 18c1.2-3 3.2-4.5 5.5-4.5S13.3 15 14.5 18" stroke={props.color ?? '#64748B'} strokeWidth={1.7} strokeLinecap="round" />
        <Circle cx="17" cy="9" r="2.2" stroke={props.color ?? '#64748B'} strokeWidth={1.7} />
        <Path d="M16 13.5c1.8.3 3.2 1.5 4 3.5" stroke={props.color ?? '#64748B'} strokeWidth={1.7} strokeLinecap="round" />
      </>
    ),
  });
}

export function AdminIconProviders(props: IconProps) {
  return base({
    ...props,
    children: (
      <>
        <Path d="M8 20V9l4-3 4 3v11" stroke={props.color ?? '#64748B'} strokeWidth={1.7} strokeLinejoin="round" />
        <Path d="M10.5 12h3M10.5 15h3M12 12v6" stroke={props.color ?? '#64748B'} strokeWidth={1.7} strokeLinecap="round" />
      </>
    ),
  });
}

export function AdminIconShield(props: IconProps) {
  return base({
    ...props,
    children: (
      <Path
        d="M12 3l7 3v5c0 4.5-3 7.8-7 9-4-1.2-7-4.5-7-9V6l7-3z"
        stroke={props.color ?? '#64748B'}
        strokeWidth={1.7}
        strokeLinejoin="round"
      />
    ),
  });
}

export function AdminIconOps(props: IconProps) {
  return base({
    ...props,
    children: (
      <>
        <Circle cx="12" cy="12" r="3" stroke={props.color ?? '#64748B'} strokeWidth={1.7} />
        <Path
          d="M12 3v2.2M12 18.8V21M3 12h2.2M18.8 12H21M5.6 5.6l1.6 1.6M16.8 16.8l1.6 1.6M18.4 5.6l-1.6 1.6M7.2 16.8l-1.6 1.6"
          stroke={props.color ?? '#64748B'}
          strokeWidth={1.7}
          strokeLinecap="round"
        />
      </>
    ),
  });
}

export function AdminIconFinance(props: IconProps) {
  return base({
    ...props,
    children: (
      <>
        <Rect x="3" y="6" width="18" height="12" rx="2.5" stroke={props.color ?? '#64748B'} strokeWidth={1.7} />
        <Path d="M3 10h18" stroke={props.color ?? '#64748B'} strokeWidth={1.7} />
        <Circle cx="16" cy="14" r="1.4" fill={props.color ?? '#64748B'} />
      </>
    ),
  });
}

export function AdminIconSupport(props: IconProps) {
  return base({
    ...props,
    children: (
      <Path
        d="M5 18l1.2-3.2A7 7 0 1112 19H7l-2-1z"
        stroke={props.color ?? '#64748B'}
        strokeWidth={1.7}
        strokeLinejoin="round"
      />
    ),
  });
}

export function AdminIconStar(props: IconProps) {
  return base({
    ...props,
    children: (
      <Path
        d="M12 3.5l2.4 4.9 5.4.8-3.9 3.8.9 5.4L12 16.8 7.2 18.4l.9-5.4L4.2 9.2l5.4-.8L12 3.5z"
        stroke={props.color ?? '#64748B'}
        strokeWidth={1.7}
        strokeLinejoin="round"
      />
    ),
  });
}

export function AdminIconGrowth(props: IconProps) {
  return base({
    ...props,
    children: (
      <Path d="M4 16l5-5 3 3 7-7M14 7h5v5" stroke={props.color ?? '#64748B'} strokeWidth={1.7} strokeLinecap="round" strokeLinejoin="round" />
    ),
  });
}

export function AdminIconTenants(props: IconProps) {
  return base({
    ...props,
    children: (
      <>
        <Rect x="4" y="8" width="7" height="11" rx="1.5" stroke={props.color ?? '#64748B'} strokeWidth={1.7} />
        <Rect x="13" y="5" width="7" height="14" rx="1.5" stroke={props.color ?? '#64748B'} strokeWidth={1.7} />
      </>
    ),
  });
}

export function AdminIconAutomation(props: IconProps) {
  return base({
    ...props,
    children: (
      <Path
        d="M7 7h4l2 3h4l2 3v4H5v-4l2-3V7zM9 17v2M15 17v2"
        stroke={props.color ?? '#64748B'}
        strokeWidth={1.7}
        strokeLinejoin="round"
      />
    ),
  });
}

export function AdminIconPartners(props: IconProps) {
  return base({
    ...props,
    children: (
      <Path
        d="M8 12a3 3 0 110-6 3 3 0 010 6zm8 0a3 3 0 110-6 3 3 0 010 6zM4.5 18c.8-2.2 2.4-3.5 4.5-3.5h1M14 14.5h1c2.1 0 3.7 1.3 4.5 3.5"
        stroke={props.color ?? '#64748B'}
        strokeWidth={1.7}
        strokeLinecap="round"
      />
    ),
  });
}

export function AdminIconConfig(props: IconProps) {
  return base({
    ...props,
    children: (
      <>
        <Circle cx="12" cy="12" r="3" stroke={props.color ?? '#64748B'} strokeWidth={1.7} />
        <Path
          d="M12 3.5v2M12 18.5v2M4.8 6.5l1.5 1.5M17.7 16l1.5 1.5M3.5 12h2M18.5 12h2M4.8 17.5l1.5-1.5M17.7 8l1.5-1.5"
          stroke={props.color ?? '#64748B'}
          strokeWidth={1.7}
          strokeLinecap="round"
        />
      </>
    ),
  });
}

export function AdminIconAudit(props: IconProps) {
  return base({
    ...props,
    children: (
      <>
        <Path d="M8 4h7l3 3v12a2 2 0 01-2 2H8a2 2 0 01-2-2V6a2 2 0 012-2z" stroke={props.color ?? '#64748B'} strokeWidth={1.7} strokeLinejoin="round" />
        <Path d="M9 11h6M9 14h6M9 17h4" stroke={props.color ?? '#64748B'} strokeWidth={1.7} strokeLinecap="round" />
      </>
    ),
  });
}

export function AdminIconMenu(props: IconProps) {
  return base({
    ...props,
    children: (
      <Path d="M4 7h16M4 12h16M4 17h16" stroke={props.color ?? '#64748B'} strokeWidth={1.7} strokeLinecap="round" />
    ),
  });
}

export function AdminIconSearch(props: IconProps) {
  return base({
    ...props,
    children: (
      <>
        <Circle cx="11" cy="11" r="6" stroke={props.color ?? '#64748B'} strokeWidth={1.7} />
        <Path d="M16 16l4 4" stroke={props.color ?? '#64748B'} strokeWidth={1.7} strokeLinecap="round" />
      </>
    ),
  });
}

export function AdminIconBell(props: IconProps) {
  return base({
    ...props,
    children: (
      <>
        <Path
          d="M6 16V10a6 6 0 1112 0v6l1.2 1.6a.9.9 0 01-.75 1.4H5.55a.9.9 0 01-.75-1.4L6 16z"
          stroke={props.color ?? '#64748B'}
          strokeWidth={1.7}
          strokeLinejoin="round"
        />
        <Path d="M10 19a2 2 0 004 0" stroke={props.color ?? '#64748B'} strokeWidth={1.7} strokeLinecap="round" />
      </>
    ),
  });
}

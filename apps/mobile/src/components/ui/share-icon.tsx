import Svg, { Circle, Path } from 'react-native-svg';

export function IconShare({ size = 20, color = '#356F5E' }: { size?: number; color?: string }) {
  return (
    <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <Circle cx="18" cy="5" r="2.5" stroke={color} strokeWidth={1.7} />
      <Circle cx="6" cy="12" r="2.5" stroke={color} strokeWidth={1.7} />
      <Circle cx="18" cy="19" r="2.5" stroke={color} strokeWidth={1.7} />
      <Path d="M8.4 13.2l7.2 4.1M15.6 6.7l-7.2 4.1" stroke={color} strokeWidth={1.7} strokeLinecap="round" />
    </Svg>
  );
}

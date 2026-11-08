import { useWindowDimensions, View } from 'react-native';
import Svg, { Circle, Path, Rect } from 'react-native-svg';

/** Compact shield / lock illustration for Super Admin login. */
export function AdminSecurityHero({ height = 112 }: { height?: number }) {
  const { width: screenWidth } = useWindowDimensions();
  const maxWidth = Math.min(screenWidth - 48, 280);
  const svgHeight = Math.min(height, 120);
  const svgWidth = Math.round(svgHeight * 1.45);

  return (
    <View
      accessible={false}
      style={{
        width: '100%',
        maxWidth,
        height: svgHeight,
        alignSelf: 'center',
        alignItems: 'center',
        justifyContent: 'center',
        borderRadius: 20,
        backgroundColor: '#EAF3EF',
        overflow: 'hidden',
      }}>
      <Svg width={svgWidth} height={svgHeight} viewBox="0 0 200 140">
        <Circle cx="100" cy="72" r="48" fill="#FFFFFF" opacity={0.7} />
        <Path
          d="M100 28c-22 10-36 12-36 12v28c0 24 16 42 36 48 20-6 36-24 36-48V40s-14-2-36-12z"
          fill="#356F5E"
        />
        <Path
          d="M100 36c-16 8-26 9-26 9v23c0 18 12 32 26 37 14-5 26-19 26-37V45s-10-1-26-9z"
          fill="#EAF3EF"
        />
        <Rect x="88" y="62" width="24" height="22" rx="4" fill="#356F5E" />
        <Path
          d="M94 62v-6a6 6 0 0 1 12 0v6"
          stroke="#356F5E"
          strokeWidth={3.5}
          strokeLinecap="round"
          fill="none"
        />
        <Circle cx="100" cy="72" r="2.5" fill="#EAF3EF" />
        <Path d="M100 74.5v5" stroke="#EAF3EF" strokeWidth={2.5} strokeLinecap="round" />
        <Circle cx="42" cy="40" r="6" fill="#356F5E" opacity={0.18} />
        <Circle cx="164" cy="48" r="5" fill="#356F5E" opacity={0.14} />
        <Circle cx="158" cy="104" r="4" fill="#356F5E" opacity={0.12} />
      </Svg>
    </View>
  );
}

import { Image } from 'expo-image';
import { LinearGradient } from 'expo-linear-gradient';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState } from 'react';
import { AccessibilityInfo, StyleSheet, Text, useWindowDimensions, View } from 'react-native';
import Animated, {
  Easing,
  FadeIn,
  FadeOut,
  interpolate,
  useAnimatedStyle,
  useSharedValue,
  withDelay,
  withRepeat,
  withSequence,
  withTiming,
} from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import Svg, { Circle, Path } from 'react-native-svg';

import { splashPalette } from '@/theme/tokens';

const splashMark = require('../../../assets/images/branding/prodoc-splash-mark.jpg');
const splashDoctor = require('../../../assets/images/branding/splash-doctor.jpg');

type PremiumSplashScreenProps = {
  /** Fires once the first layout is painted so the native splash can hide. */
  onFirstFrame?: () => void;
  message?: string;
  brand?: string;
  tagline?: string;
};

function FloatingOrb({
  size,
  top,
  left,
  delay,
  reduceMotion,
}: {
  size: number;
  top: number;
  left: number;
  delay: number;
  reduceMotion: boolean;
}) {
  const drift = useSharedValue(0);

  useEffect(() => {
    if (reduceMotion) {
      return;
    }
    drift.value = withDelay(
      delay,
      withRepeat(
        withSequence(
          withTiming(1, { duration: 2800, easing: Easing.inOut(Easing.sin) }),
          withTiming(0, { duration: 2800, easing: Easing.inOut(Easing.sin) }),
        ),
        -1,
        false,
      ),
    );
  }, [delay, drift, reduceMotion]);

  const style = useAnimatedStyle(() => ({
    opacity: interpolate(drift.value, [0, 1], [0.22, 0.48]),
    transform: [{ translateY: interpolate(drift.value, [0, 1], [0, -10]) }],
  }));

  return (
    <Animated.View
      pointerEvents="none"
      style={[
        {
          position: 'absolute',
          top,
          left,
          width: size,
          height: size,
          borderRadius: size / 2,
          backgroundColor: splashPalette.orb,
        },
        style,
      ]}
    />
  );
}

function FloatingCross({
  top,
  left,
  right,
  delay,
  reduceMotion,
}: {
  top: number;
  left?: number;
  right?: number;
  delay: number;
  reduceMotion: boolean;
}) {
  const pulse = useSharedValue(0);

  useEffect(() => {
    if (reduceMotion) {
      return;
    }
    pulse.value = withDelay(
      delay,
      withRepeat(
        withSequence(
          withTiming(1, { duration: 2200, easing: Easing.inOut(Easing.quad) }),
          withTiming(0, { duration: 2200, easing: Easing.inOut(Easing.quad) }),
        ),
        -1,
        false,
      ),
    );
  }, [delay, pulse, reduceMotion]);

  const style = useAnimatedStyle(() => ({
    opacity: interpolate(pulse.value, [0, 1], [0.18, 0.42]),
    transform: [
      { translateY: interpolate(pulse.value, [0, 1], [0, -8]) },
      { scale: interpolate(pulse.value, [0, 1], [0.92, 1.05]) },
    ],
  }));

  return (
    <Animated.View pointerEvents="none" style={[{ position: 'absolute', top, left, right }, style]}>
      <Svg width={28} height={28} viewBox="0 0 28 28">
        <Circle cx="14" cy="14" r="14" fill={splashPalette.crossPlate} />
        <Path
          d="M14 7v14M7 14h14"
          stroke={splashPalette.cross}
          strokeWidth={2.6}
          strokeLinecap="round"
        />
      </Svg>
    </Animated.View>
  );
}

function IndeterminateLoader({ reduceMotion }: { reduceMotion: boolean }) {
  const progress = useSharedValue(0);

  useEffect(() => {
    if (reduceMotion) {
      progress.value = 0.45;
      return;
    }
    progress.value = withRepeat(
      withSequence(
        withTiming(1, { duration: 1100, easing: Easing.inOut(Easing.cubic) }),
        withTiming(0, { duration: 1100, easing: Easing.inOut(Easing.cubic) }),
      ),
      -1,
      false,
    );
  }, [progress, reduceMotion]);

  const fillStyle = useAnimatedStyle(() => ({
    width: `${interpolate(progress.value, [0, 1], [28, 86])}%`,
    opacity: interpolate(progress.value, [0, 0.5, 1], [0.75, 1, 0.75]),
  }));

  return (
    <View style={styles.loaderTrack} accessibilityRole="progressbar" accessibilityLabel="Loading">
      <Animated.View style={[styles.loaderFill, fillStyle]} />
    </View>
  );
}

/** Branded startup surface shown while fonts, session, and runtime bootstrap. */
export function PremiumSplashScreen({
  onFirstFrame,
  brand = 'ProDoc',
  tagline = 'Your Health, Our Priority',
  message = 'Preparing your healthcare experience...',
}: PremiumSplashScreenProps) {
  const insets = useSafeAreaInsets();
  const { width, height } = useWindowDimensions();
  const [reduceMotion, setReduceMotion] = useState(false);
  const logoProgress = useSharedValue(0);
  const heroProgress = useSharedValue(0);
  const copyProgress = useSharedValue(0);

  useEffect(() => {
    let active = true;
    void AccessibilityInfo.isReduceMotionEnabled().then((enabled) => {
      if (active) {
        setReduceMotion(enabled);
      }
    });
    const sub = AccessibilityInfo.addEventListener('reduceMotionChanged', setReduceMotion);
    return () => {
      active = false;
      sub.remove();
    };
  }, []);

  // Hide the native splash as soon as this branded surface mounts (avoid white flash).
  useEffect(() => {
    onFirstFrame?.();
  }, [onFirstFrame]);

  useEffect(() => {
    if (reduceMotion) {
      logoProgress.value = 1;
      heroProgress.value = 1;
      copyProgress.value = 1;
      return;
    }
    logoProgress.value = withTiming(1, { duration: 520, easing: Easing.out(Easing.cubic) });
    heroProgress.value = withDelay(140, withTiming(1, { duration: 560, easing: Easing.out(Easing.cubic) }));
    copyProgress.value = withDelay(260, withTiming(1, { duration: 480, easing: Easing.out(Easing.cubic) }));
  }, [copyProgress, heroProgress, logoProgress, reduceMotion]);

  const logoStyle = useAnimatedStyle(() => ({
    opacity: logoProgress.value,
    transform: [{ scale: interpolate(logoProgress.value, [0, 1], [0.86, 1]) }],
  }));

  const heroStyle = useAnimatedStyle(() => ({
    opacity: heroProgress.value,
    transform: [{ translateY: interpolate(heroProgress.value, [0, 1], [18, 0]) }],
  }));

  const copyStyle = useAnimatedStyle(() => ({
    opacity: copyProgress.value,
    transform: [{ translateY: interpolate(copyProgress.value, [0, 1], [12, 0]) }],
  }));

  const doctorWidth = Math.min(width * 0.72, 280);
  const doctorHeight = Math.min(height * 0.34, doctorWidth * 1.15);

  return (
    <Animated.View
      entering={reduceMotion ? undefined : FadeIn.duration(180)}
      exiting={reduceMotion ? undefined : FadeOut.duration(220)}
      onLayout={() => onFirstFrame?.()}
      style={styles.root}
      accessibilityLabel={`${brand}. ${tagline}`}>
      <StatusBar style="light" />
      <LinearGradient
        colors={[splashPalette.mid, splashPalette.deep, '#1B3F35']}
        locations={[0, 0.55, 1]}
        start={{ x: 0.15, y: 0 }}
        end={{ x: 0.9, y: 1 }}
        style={StyleSheet.absoluteFill}
      />

      <View style={StyleSheet.absoluteFill} pointerEvents="none">
        <View style={[styles.glow, styles.glowTop]} />
        <View style={[styles.glow, styles.glowBottom]} />
        <FloatingOrb size={140} top={height * 0.08} left={-36} delay={0} reduceMotion={reduceMotion} />
        <FloatingOrb size={96} top={height * 0.58} left={width * 0.72} delay={400} reduceMotion={reduceMotion} />
        <FloatingOrb size={64} top={height * 0.28} left={width * 0.78} delay={700} reduceMotion={reduceMotion} />
        <FloatingCross top={height * 0.18} right={28} delay={200} reduceMotion={reduceMotion} />
        <FloatingCross top={height * 0.64} left={28} delay={900} reduceMotion={reduceMotion} />
      </View>

      <View
        style={[
          styles.shell,
          {
            paddingTop: Math.max(insets.top, 24),
            paddingBottom: Math.max(insets.bottom, 28),
          },
        ]}>
        <View style={styles.content}>
          <Animated.View style={[styles.logoWrap, logoStyle]}>
            <Image
              source={splashMark}
              style={styles.logo}
              contentFit="contain"
              transition={0}
              accessibilityIgnoresInvertColors
            />
          </Animated.View>

          <Animated.View style={[styles.heroWrap, { width: doctorWidth, height: doctorHeight }, heroStyle]}>
            <Image
              source={splashDoctor}
              style={styles.heroImage}
              contentFit="cover"
              transition={0}
              accessibilityIgnoresInvertColors
            />
          </Animated.View>

          <Animated.View style={[styles.copyBlock, copyStyle]}>
            <Text style={styles.brand}>{brand}</Text>
            <Text style={styles.tagline}>{tagline}</Text>
          </Animated.View>
        </View>

        <View style={styles.footer}>
          <IndeterminateLoader reduceMotion={reduceMotion} />
          <Text style={styles.message}>{message}</Text>
        </View>
      </View>
    </Animated.View>
  );
}

const styles = StyleSheet.create({
  root: {
    flex: 1,
    backgroundColor: splashPalette.deep,
  },
  shell: {
    flex: 1,
    justifyContent: 'space-between',
    paddingHorizontal: 28,
  },
  glow: {
    position: 'absolute',
    borderRadius: 999,
  },
  glowTop: {
    width: 320,
    height: 320,
    top: -80,
    alignSelf: 'center',
    left: '50%',
    marginLeft: -160,
    backgroundColor: splashPalette.glow,
  },
  glowBottom: {
    width: 280,
    height: 280,
    bottom: -40,
    right: -60,
    backgroundColor: splashPalette.glowSoft,
  },
  content: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 22,
  },
  logoWrap: {
    width: 88,
    height: 88,
    borderRadius: 28,
    overflow: 'hidden',
    backgroundColor: splashPalette.markPlate,
    shadowColor: '#0B2A22',
    shadowOffset: { width: 0, height: 10 },
    shadowOpacity: 0.28,
    shadowRadius: 18,
    elevation: 8,
  },
  logo: {
    width: '100%',
    height: '100%',
  },
  heroWrap: {
    borderRadius: 28,
    overflow: 'hidden',
    backgroundColor: splashPalette.mint,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.18)',
  },
  heroImage: {
    width: '100%',
    height: '100%',
  },
  copyBlock: {
    alignItems: 'center',
    gap: 8,
    paddingHorizontal: 12,
  },
  brand: {
    fontSize: 40,
    lineHeight: 46,
    fontWeight: '700',
    letterSpacing: -0.8,
    color: splashPalette.onDeep,
  },
  tagline: {
    fontSize: 16,
    lineHeight: 22,
    fontWeight: '500',
    letterSpacing: 0.2,
    color: splashPalette.mint,
    textAlign: 'center',
  },
  footer: {
    alignItems: 'center',
    gap: 14,
    paddingBottom: 8,
  },
  loaderTrack: {
    width: '72%',
    maxWidth: 240,
    height: 6,
    borderRadius: 999,
    backgroundColor: 'rgba(234, 243, 239, 0.22)',
    overflow: 'hidden',
  },
  loaderFill: {
    height: '100%',
    borderRadius: 999,
    backgroundColor: splashPalette.mint,
  },
  message: {
    fontSize: 13,
    lineHeight: 18,
    fontWeight: '500',
    color: 'rgba(234, 243, 239, 0.82)',
    textAlign: 'center',
  },
});

import { useQuery } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useMemo, useState } from 'react';
import { Pressable, View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useActiveLanguage, useCopy } from '@/i18n/copy';
import { getHelpArticle, listHelpCategories, searchHelpArticles } from '@/services/api/support-api';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

export function HelpCenter() {
  const copy = useCopy();
  const router = useRouter();
  const language = useActiveLanguage();
  const [query, setQuery] = useState('');
  const [category, setCategory] = useState<string | undefined>();
  const [selectedSlug, setSelectedSlug] = useState<string | null>(null);

  const categories = useQuery({ queryKey: ['help', 'categories'], queryFn: listHelpCategories });
  const articles = useQuery({
    queryKey: ['help', 'articles', query, category],
    queryFn: () => searchHelpArticles(query.trim() || undefined, category),
  });
  const article = useQuery({
    queryKey: ['help', 'article', selectedSlug],
    queryFn: () => getHelpArticle(selectedSlug!),
    enabled: Boolean(selectedSlug),
  });

  const categoryName = useMemo(() => {
    const map = new Map(
      (categories.data ?? []).map((item) => [item.code, language === 'hi' ? item.nameHi : item.nameEn]),
    );
    return (code: string) => map.get(code) ?? code;
  }, [categories.data, language]);

  const error = categories.error ?? articles.error ?? article.error;

  return (
    <AuthScaffold>
      <Button label={copy.back} variant="secondary" onPress={() => router.back()} />
      <AppText variant="title">{copy.helpCenterTitle}</AppText>
      <AppText variant="muted">{copy.helpCenterBody}</AppText>
      <Card>
        <TextField label={copy.helpSearch} value={query} onChangeText={setQuery} placeholder={copy.helpSearchPlaceholder} />
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
          <Button
            label={copy.helpAllCategories}
            variant={category ? 'secondary' : 'primary'}
            onPress={() => setCategory(undefined)}
          />
          {(categories.data ?? []).map((item) => (
            <Button
              key={item.id}
              label={language === 'hi' ? item.nameHi : item.nameEn}
              variant={category === item.code ? 'primary' : 'secondary'}
              onPress={() => setCategory(item.code)}
            />
          ))}
        </View>
      </Card>
      <Card>
        {(articles.data ?? []).map((item) => (
          <Pressable key={item.id} onPress={() => setSelectedSlug(item.slug)} style={{ gap: spacing.xs }}>
            <AppText>{language === 'hi' ? item.titleHi : item.titleEn}</AppText>
            <AppText variant="muted">{categoryName(item.categoryCode)}</AppText>
          </Pressable>
        ))}
        {articles.data?.length === 0 ? <AppText variant="muted">{copy.helpEmpty}</AppText> : null}
      </Card>
      {article.data ? (
        <Card>
          <AppText variant="title">{language === 'hi' ? article.data.titleHi : article.data.titleEn}</AppText>
          <AppText variant="muted">{language === 'hi' ? article.data.bodyHi : article.data.bodyEn}</AppText>
          <Button label={copy.supportCenter} onPress={() => router.push('/support' as Href)} />
        </Card>
      ) : null}
      {error ? <AppText variant="muted">{problemMessage(error, copy.helpCenterTitle)}</AppText> : null}
    </AuthScaffold>
  );
}

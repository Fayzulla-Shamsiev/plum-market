// Placeholder artwork for products and categories the merchant hasn't uploaded a photo for yet.
// Matches keywords in any of the three catalog languages; real photos always win.
const rules: [RegExp, string][] = [
  [/круасс|kruass|круас/i, '🥐'],
  [/самс|soms|сомс|пирож(?!н)/i, '🥟'],
  [/пицц|пица|pits/i, '🍕'],
  [/бургер|burger|фастфуд|fastfud/i, '🍔'],
  [/лаваш|lavash|шаурм|shaurm/i, '🌯'],
  [/хот-дог|xot-dog|хот/i, '🌭'],
  [/сэндвич|sendvich|сендвич/i, '🥪'],
  [/капуч|kapuch|латте|latte|американ|amerikan|раф|raf|кофе|qahva|қаҳва/i, '☕'],
  [/лимонад|limonad/i, '🍋'],
  [/(^|[^а-яё])сок|sharbat|шарбат|фреш|fresh/i, '🧃'], // not the «сок» inside «кусок»
  [/чай|choy|чой/i, '🍵'],
  [/напит|ichimlik|ичимлик/i, '🥤'],
  [/чизкейк|chizkeyk/i, '🍰'],
  [/медов|asalli|асалли/i, '🍯'],
  [/тирамису|tiramisu/i, '🍮'],
  [/маффин|maffin/i, '🧁'],
  [/эклер|ekler/i, '🍫'],
  [/торт|tort/i, '🎂'],
  [/пирожн|pirojn|десерт|desert/i, '🧁'],
  [/багет|baget/i, '🥖'],
  [/лепёш|лепеш|tandir|тандир/i, '🫓'],
  [/хлеб|non\b|нон\b|выпеч|pishiriq|пишириқ/i, '🍞'],
  [/салат|salat/i, '🥗'],
  [/суп|shoʻrva|sho'rva|шўрва/i, '🍲'],
]

export function emojiFor(name: string): string {
  return rules.find(([re]) => re.test(name))?.[1] ?? '🛍️'
}

// Soft backgrounds so a grid of placeholders doesn't look like one flat block.
const tints = ['#eef4ff', '#eafaf1', '#fff5e6', '#fdeef3', '#f1effd', '#e9f7fb', '#f6f7e8']
export const tintFor = (id: number) => tints[id % tints.length]

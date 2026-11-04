const fs = require('node:fs/promises');
const path = require('node:path');

module.exports = async function checkLayout(browser, baseUrl = 'http://127.0.0.1:5187') {
  const output = path.join(__dirname, 'bin', 'layout-checks');
  await fs.mkdir(output, { recursive: true });
  const cases = [
    { name: 'desktop', width: 1120, height: 680, mode: '' },
    { name: 'reference-window', width: 1094, height: 655, mode: 'dualtype', scale: 1.5, webFonts: true },
    { name: 'reference-window-offline', width: 1094, height: 655, mode: 'dualtype', scale: 1.5 },
    { name: 'compact', width: 1000, height: 620, mode: 'pokerus' },
    { name: 'english', width: 900, height: 680, mode: 'english' },
    { name: 'narrow', width: 760, height: 620, mode: 'warning' },
    { name: 'mobile', width: 390, height: 844, mode: 'english' },
    { name: 'small', width: 320, height: 740, mode: 'pokerus' },
    { name: 'dark', width: 1120, height: 680, mode: 'warning', colorScheme: 'dark' },
    { name: 'empty', width: 1000, height: 620, mode: 'empty' },
    { name: 'loading', width: 1000, height: 620, mode: 'loading' },
    { name: 'toast', width: 1000, height: 620, mode: 'toast' },
    { name: 'hatch', width: 1120, height: 680, mode: 'hatch' },
    { name: 'dpi125-hd', width: 1052, height: 520, mode: 'english', scale: 1.25 },
    { name: 'dpi150-fhd', width: 1120, height: 640, mode: '', scale: 1.5 },
    { name: 'dpi175-fhd', width: 1080, height: 550, mode: 'pokerus', scale: 1.75 },
    { name: 'dpi200-fhd', width: 944, height: 470, mode: 'warning', scale: 2 },
    { name: 'dpi250-qhd', width: 1008, height: 500, mode: 'english', scale: 2.5 },
    { name: 'dpi300-4k', width: 1120, height: 640, mode: '', scale: 3 },
    { name: 'dpi200-hd', width: 624, height: 280, mode: 'pokerus', scale: 2 },
    { name: 'dpi300-fhd', width: 624, height: 280, mode: 'empty', scale: 3 },
  ];
  const results = [];
  for (const test of cases) {
    const context = await browser.newContext({ viewport: { width: test.width, height: test.height }, deviceScaleFactor: test.scale || 1, colorScheme: test.colorScheme || 'light' });
    const page = await context.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    if (!test.webFonts) await page.route('https://fonts.googleapis.com/**', route => route.abort());
    await page.goto(`${baseUrl}/?case=${test.mode}`, { waitUntil: 'networkidle' });
    await page.evaluate(() => document.fonts.ready);
    if (test.webFonts && !await page.evaluate(() => ['DM Sans', 'Fira Code'].every(family => [...document.fonts].some(font => font.family === family && font.status === 'loaded')))) throw new Error(`${test.name}: web fonts did not load`);
    if (test.mode === 'toast') await page.locator('.toast').evaluate(el => { el.classList.add('visible'); getComputedStyle(el).opacity; el.getAnimations().forEach(animation => animation.finish()); });
    const measure = async () => page.evaluate(() => {
      const visible = el => {
        if (!el.getClientRects().length) return false;
        if (el.closest('details:not([open])') && !el.closest('summary')) return false;
        for (let node = el; node; node = node.parentElement) {
          const style = getComputedStyle(node);
          if (style.display === 'none' || style.visibility === 'hidden' || Number(style.opacity) === 0) return false;
        }
        return true;
      };
      const text = [...document.querySelectorAll('body *')].filter(el => visible(el) && (el.matches('input,select,button') || [...el.childNodes].some(n => n.nodeType === 3 && n.textContent.trim())));
      const smallFonts = text.filter(el => parseFloat(getComputedStyle(el).fontSize) < 12).map(el => ({ tag: el.tagName, text: el.textContent.slice(0,40), font: getComputedStyle(el).fontSize }));
      const clipped = text.filter(el => el.children.length === 0 && !el.matches('input,select,option') && el.scrollWidth > el.clientWidth + 2).map(el => ({ tag: el.tagName, text: el.textContent, width: el.clientWidth, scroll: el.scrollWidth }));
      const party = [...document.querySelectorAll('.party-slot')].map(el => el.getBoundingClientRect().toJSON());
      const stats = document.querySelector('.stats-section').getBoundingClientRect().toJSON();
      const moves = document.querySelector('.moves-section').getBoundingClientRect().toJSON();
      const images = [...document.querySelectorAll('.party-slot img')].map(img => {
        const canvas = document.createElement('canvas'); canvas.width = canvas.height = 32;
        canvas.getContext('2d').drawImage(img, 0, 0, 32, 32);
        const pixels = canvas.getContext('2d').getImageData(0, 0, 32, 32).data;
        return { loaded: img.complete && img.naturalWidth === 32, opaque: [...pixels].filter((v, i) => i % 4 === 3 && v > 0).length };
      });
      const overflowingNames = [...document.querySelectorAll('.party-name')].filter(el => {
        const rect = el.getBoundingClientRect(); const slot = el.closest('.party-slot').getBoundingClientRect();
        return rect.top < slot.top || rect.bottom > slot.bottom;
      }).length;
      const fields = document.querySelector('.editor-fields').getBoundingClientRect().toJSON();
      const footer = document.querySelector('.button-group').getBoundingClientRect().toJSON();
      const palette = getComputedStyle(document.querySelector('.trainer-shell')).getPropertyValue('--surface').trim();
      const selectFont = getComputedStyle(document.querySelector('select')).fontFamily;
      const tableHeader = getComputedStyle(document.querySelector('.stats-table thead')).backgroundColor;
      const removedDecorationCount = document.querySelectorAll('.slot-number,.party-name,.connection-state,.icon-sparkles,.ribbon-count,.process-icon,.spinner-inner').length;
      const blurredOverlays = [...document.querySelectorAll('.modal-overlay,.loading-overlay')].some(el => getComputedStyle(el).backdropFilter !== 'none');
      const modal = document.querySelector('.modal-overlay.visible .modal-content');
      const modalBounds = modal?.getBoundingClientRect().toJSON();
      const rail = document.querySelector('.party-rail').getBoundingClientRect();
      const partyInsets = { left: party[0].left - rail.left, right: rail.right - party[0].right };
      const ribbons = [...document.querySelectorAll('.ribbon-checkbox')];
      const ribbonsAlwaysShown = !document.querySelector('.ribbons-details') && ribbons.length > 0 && ribbons.every(el => visible(el));
      const ribbonsBounds = document.querySelector('.ribbons-section').getBoundingClientRect().toJSON();
      const fieldsElement = document.querySelector('.editor-fields');
      const editorScroll = fieldsElement.scrollHeight - fieldsElement.clientHeight;
      const shinyInput = document.querySelector('.shiny-toggle input');
      const shinyHidden = getComputedStyle(shinyInput).opacity === '0' && !!document.querySelector('.shiny-icon');
      const asymmetricBorders = [...document.querySelectorAll('main *, .toast')].filter(el => parseFloat(getComputedStyle(el).borderLeftWidth) > parseFloat(getComputedStyle(el).borderRightWidth)).length;
      const encounter = document.querySelector('.encounter-info').textContent.replace(/\s+/g, ' ').trim();
      return { width: innerWidth, height: innerHeight, density: devicePixelRatio, documentWidth: document.documentElement.scrollWidth, documentHeight: document.documentElement.scrollHeight, smallFonts, clipped, party, stats, moves, images, overflowingNames, fields, footer, palette, selectFont, tableHeader, removedDecorationCount, blurredOverlays, modalBounds, partyInsets, ribbonsAlwaysShown, ribbonsBounds, editorScroll, shinyHidden, asymmetricBorders, encounter };
    });
    let measurements = await measure();
    if (errors.length || measurements.smallFonts.length || measurements.clipped.length || measurements.documentWidth > test.width + 1) {
      throw new Error(`${test.name}: ${JSON.stringify({ errors, ...measurements })}`);
    }
    if (measurements.party.length !== 6 || !measurements.party.every((rect, i, all) => rect.width < 100 && (!i || rect.x === all[0].x && rect.y > all[i-1].y))) throw new Error(`${test.name}: party is not a fixed-size vertical rail`);
    if (test.width > 800 && measurements.moves.x <= measurements.stats.x) throw new Error(`${test.name}: desktop columns stacked`);
    if (test.name === 'desktop' && measurements.documentHeight > test.height + 1) throw new Error('Default desktop does not fit');
    if (measurements.overflowingNames || measurements.fields.bottom > measurements.footer.top + 1 || measurements.footer.bottom > test.height + 1) throw new Error(`${test.name}: name or action bar overlap`);
    if (measurements.density !== (test.scale || 1)) throw new Error(`${test.name}: incorrect rendering density`);
    if (measurements.modalBounds && (measurements.modalBounds.top < 0 || measurements.modalBounds.bottom > test.height)) throw new Error(`${test.name}: modal outside viewport`);
    if (test.mode !== 'empty' && !measurements.images.every(img => img.loaded && img.opaque > 20)) throw new Error(`${test.name}: missing or blank ROM icons`);
    if (measurements.palette !== (test.colorScheme === 'dark' ? '#1a1b19' : '#fdfcf7') || !measurements.selectFont.includes('Fira Code') || measurements.tableHeader !== 'rgb(122, 143, 98)') throw new Error(`${test.name}: original theme changed`);
    if (measurements.removedDecorationCount || measurements.blurredOverlays) throw new Error(`${test.name}: unwanted decorative UI remains`);
    if (Math.abs(measurements.partyInsets.left - measurements.partyInsets.right) > 1) throw new Error(`${test.name}: unequal party padding`);
    if (!measurements.ribbonsAlwaysShown || !measurements.shinyHidden || measurements.asymmetricBorders) throw new Error(`${test.name}: ribbons, shiny icon or left border regression`);
    if (['desktop', 'reference-window', 'reference-window-offline'].includes(test.name) && (measurements.editorScroll > 0 || measurements.ribbonsBounds.bottom > measurements.fields.bottom)) throw new Error(`${test.name}: default editor still scrolls: ${JSON.stringify(measurements)}`);
    if (test.mode !== 'empty' && !measurements.encounter.endsWith(test.mode === 'hatch' ? '부화했습니다.' : '로 만났습니다.')) throw new Error(`${test.name}: encounter sentence truncated`);
    await page.screenshot({ path: path.join(output, `${test.name}.png`), fullPage: true });
    if (test.mode !== 'empty' && test.mode !== 'loading') {
      await page.locator('.ribbon-checkbox').last().scrollIntoViewIfNeeded();
      measurements = await measure();
      if (measurements.documentWidth > test.width + 1 || measurements.smallFonts.length || measurements.clipped.length) throw new Error(`${test.name}: ribbon overflow`);
      await page.screenshot({ path: path.join(output, `${test.name}-ribbons.png`), fullPage: true });
    }
    results.push({ name: test.name, scale: test.scale || 1, viewport: `${test.width}x${test.height}`, document: `${measurements.documentWidth}x${measurements.documentHeight}`, minimumFont: '12px', editorScroll: measurements.editorScroll, icons: measurements.images.length });
    await context.close();
  }
  const context = await browser.newContext({ viewport: { width: 1120, height: 680 } });
  try {
    const page = await context.newPage();
    await page.route('https://fonts.googleapis.com/**', route => route.abort());
    await page.goto(`${baseUrl}/?case=pokerus`, { waitUntil: 'networkidle' });
    const shiny = page.locator('.shiny-toggle input');
    const wasShiny = await shiny.isChecked();
    const initialIconColor = await page.locator('.shiny-icon').evaluate(el => getComputedStyle(el).color);
    await page.locator('.shiny-toggle').click();
    if (await shiny.isChecked() === wasShiny || await page.locator('.shiny-icon').evaluate(el => getComputedStyle(el).color) === initialIconColor) throw new Error('Shiny icon click did not toggle state and color');
    await shiny.focus();
    await shiny.press('Space');
    if (await shiny.isChecked() !== wasShiny) throw new Error('Shiny keyboard toggle did not restore state');
    const labels = await page.locator('.condition-toggles').textContent();
    if (!labels.includes('Obedience(복종)') || !labels.includes('Pokérus(포케러스)')) throw new Error('Bilingual condition labels missing');
    const initialValues = await page.locator('input,select').evaluateAll(elements => elements.map(el => [el.value, el.checked]));
    const client = await context.newCDPSession(page);
    for (const size of [{ width: 944, height: 470, scale: 2 }, { width: 624, height: 280, scale: 3 }, { width: 1120, height: 680, scale: 1 }]) {
      await client.send('Emulation.setDeviceMetricsOverride', { width: size.width, height: size.height, deviceScaleFactor: size.scale, mobile: false });
      await page.evaluate(() => new Promise(resolve => requestAnimationFrame(resolve)));
      const result = await page.evaluate(() => {
        const footer = document.querySelector('.button-group').getBoundingClientRect();
        const fields = document.querySelector('.editor-fields').getBoundingClientRect();
        return { density: devicePixelRatio, overflow: document.documentElement.scrollWidth > innerWidth, footerBottom: footer.bottom, fieldsBottom: fields.bottom, footerTop: footer.top, height: innerHeight };
      });
      if (result.density !== size.scale || result.overflow || result.footerBottom > result.height || result.fieldsBottom > result.footerTop) throw new Error(`Live scale change: ${JSON.stringify(result)}`);
      const values = await page.locator('input,select').evaluateAll(elements => elements.map(el => [el.value, el.checked]));
      if (JSON.stringify(values) !== JSON.stringify(initialValues)) throw new Error('Form values changed after resizing');
      await page.locator('.party-slot').last().scrollIntoViewIfNeeded();
      if (!await page.locator('.party-slot').last().evaluate(el => { const rect = el.getBoundingClientRect(); return rect.top >= 0 && rect.bottom <= innerHeight; })) throw new Error('Last party slot inaccessible after resizing');
      await page.locator('.ribbon-checkbox').last().scrollIntoViewIfNeeded();
      if (!await page.locator('.ribbon-checkbox').last().evaluate(el => { const rect = el.getBoundingClientRect(); return rect.top >= 0 && rect.bottom <= innerHeight; })) throw new Error('Last editor fields inaccessible after resizing');
    }
    results.push({ name: 'live-scale-change', scales: '100% -> 200% -> 300% -> 100%', formValuesPreserved: true });
  } finally {
    await context.close();
  }
  return { results, screenshots: output };
};

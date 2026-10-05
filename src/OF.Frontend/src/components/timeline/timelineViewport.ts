export function getTimelineDateAtPosition(
  dates: readonly Date[] | undefined,
  columnWidth: number | undefined,
  position: number,
): number | null {
  if (!dates?.length || !columnWidth || columnWidth <= 0) return null;

  const fractionalIndex = Math.min(dates.length - 1, Math.max(0, position / columnWidth));
  const lowerIndex = Math.floor(fractionalIndex);
  const upperIndex = Math.min(dates.length - 1, lowerIndex + 1);
  const lowerTime = dates[lowerIndex].getTime();
  const upperTime = dates[upperIndex].getTime();

  return lowerTime + (upperTime - lowerTime) * (fractionalIndex - lowerIndex);
}

export function getTimelinePositionForDate(
  dates: readonly Date[] | undefined,
  columnWidth: number | undefined,
  date: number,
): number | null {
  if (!dates?.length || !columnWidth || columnWidth <= 0) return null;

  const firstTime = dates[0].getTime();
  const lastIndex = dates.length - 1;
  const lastTime = dates[lastIndex].getTime();
  if (date <= firstTime) return 0;
  if (date >= lastTime) return lastIndex * columnWidth;

  let lowerIndex = 0;
  let upperIndex = lastIndex;
  while (upperIndex - lowerIndex > 1) {
    const middleIndex = Math.floor((lowerIndex + upperIndex) / 2);
    if (dates[middleIndex].getTime() <= date) {
      lowerIndex = middleIndex;
    } else {
      upperIndex = middleIndex;
    }
  }

  const lowerTime = dates[lowerIndex].getTime();
  const upperTime = dates[upperIndex].getTime();
  const fraction = upperTime === lowerTime ? 0 : (date - lowerTime) / (upperTime - lowerTime);
  return (lowerIndex + fraction) * columnWidth;
}

export function getVisibleTimelineLabelStart(
  barStart: number,
  barWidth: number,
  labelWidth: number,
  viewportStart: number,
  viewportEnd: number,
): number | null {
  const visibleStart = Math.max(barStart, viewportStart);
  const visibleEnd = Math.min(barStart + barWidth, viewportEnd);
  if (visibleEnd <= visibleStart) return null;

  const centredStart = visibleStart + Math.max(0, (visibleEnd - visibleStart - labelWidth) / 2);
  const latestReadableStart = Math.max(viewportStart, viewportEnd - labelWidth);
  return Math.min(Math.max(centredStart, viewportStart), latestReadableStart);
}

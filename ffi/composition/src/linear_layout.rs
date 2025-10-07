use crate::constraints::ScalarConstraint;

const EPSILON: f64 = 1e-6;

#[derive(Clone, Copy, Debug)]
pub struct LinearLayoutItem {
    pub constraint: ScalarConstraint,
    pub weight: f64,
    pub margin_leading: f64,
    pub margin_trailing: f64,
}

impl LinearLayoutItem {
    pub fn new(constraint: ScalarConstraint) -> Self {
        Self {
            constraint,
            weight: 1.0,
            margin_leading: 0.0,
            margin_trailing: 0.0,
        }
    }

    pub fn with_weight(mut self, weight: f64) -> Self {
        self.weight = sanitise_weight(weight);
        self
    }

    pub fn with_margins(mut self, leading: f64, trailing: f64) -> Self {
        self.margin_leading = leading.max(0.0);
        self.margin_trailing = trailing.max(0.0);
        self
    }
}

#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct LinearLayoutSlot {
    pub offset: f64,
    pub length: f64,
}

pub fn solve_linear_layout(
    items: &[LinearLayoutItem],
    available: f64,
    spacing: f64,
) -> Vec<LinearLayoutSlot> {
    if items.is_empty() {
        return Vec::new();
    }

    let spacing = spacing.max(0.0);
    let item_count = items.len();
    let spacing_total = spacing * (item_count.saturating_sub(1) as f64);
    let mut margin_total = 0.0;

    #[derive(Clone, Debug)]
    struct ItemInfo {
        min: f64,
        preferred: f64,
        max: f64,
        weight: f64,
        margin_leading: f64,
        margin_trailing: f64,
    }

    let mut infos = Vec::with_capacity(item_count);
    let mut min_sum = 0.0;
    let mut pref_sum = 0.0;
    let mut max_sum = 0.0;
    let mut shrink_capacity_sum = 0.0;
    let mut grow_capacity_sum = 0.0;
    let mut has_infinite_capacity = false;

    for item in items {
        let constraint = item.constraint.normalised();
        let (min, preferred, max) = constraint.span();
        let weight = sanitise_weight(item.weight);
        let margin_leading = item.margin_leading.max(0.0);
        let margin_trailing = item.margin_trailing.max(0.0);

        margin_total += margin_leading + margin_trailing;
        min_sum += min;
        pref_sum += preferred;

        if max.is_finite() {
            max_sum += max;
            grow_capacity_sum += (max - preferred).max(0.0) * weight;
        } else {
            has_infinite_capacity = true;
        }

        shrink_capacity_sum += (preferred - min).max(0.0) * weight;

        infos.push(ItemInfo {
            min,
            preferred,
            max,
            weight,
            margin_leading,
            margin_trailing,
        });
    }

    let mut available = sanitise_dimension(available);
    if available.is_infinite() {
        available = pref_sum + margin_total + spacing_total;
    }
    let available_for_lengths = (available - margin_total - spacing_total).max(0.0);

    let mut lengths = vec![0.0f64; item_count];

    if available_for_lengths <= min_sum + EPSILON {
        for (index, info) in infos.iter().enumerate() {
            lengths[index] = info.min;
        }
    } else if !has_infinite_capacity && available_for_lengths >= max_sum - EPSILON {
        for (index, info) in infos.iter().enumerate() {
            lengths[index] = info.max;
        }
    } else if pref_sum > available_for_lengths + EPSILON {
        let deficit = pref_sum - available_for_lengths;
        if shrink_capacity_sum <= EPSILON {
            let scale = available_for_lengths / pref_sum.max(EPSILON);
            for (index, info) in infos.iter().enumerate() {
                lengths[index] = (info.preferred * scale).clamp(info.min, info.max);
            }
        } else {
            for (index, info) in infos.iter().enumerate() {
                let capacity = (info.preferred - info.min).max(0.0) * info.weight;
                let shrink = if capacity <= EPSILON {
                    0.0
                } else {
                    deficit * (capacity / shrink_capacity_sum)
                };
                lengths[index] = (info.preferred - shrink).max(info.min);
            }
        }
    } else {
        let extra = (available_for_lengths - pref_sum).max(0.0);
        if extra <= EPSILON {
            for (index, info) in infos.iter().enumerate() {
                lengths[index] = info.preferred;
            }
        } else if has_infinite_capacity {
            let mut weight_sum = 0.0;
            for info in &infos {
                if !info.max.is_finite() {
                    weight_sum += info.weight;
                }
            }
            weight_sum = weight_sum.max(EPSILON);
            for (index, info) in infos.iter().enumerate() {
                if info.max.is_finite() {
                    lengths[index] = info.preferred;
                } else {
                    let share = info.weight / weight_sum;
                    lengths[index] = info.preferred + extra * share;
                }
            }
        } else if grow_capacity_sum <= EPSILON {
            for (index, info) in infos.iter().enumerate() {
                lengths[index] = info.max.min(info.preferred + extra / item_count as f64);
            }
        } else {
            for (index, info) in infos.iter().enumerate() {
                let capacity = (info.max - info.preferred).max(0.0) * info.weight;
                let growth = if capacity <= EPSILON {
                    0.0
                } else {
                    extra * (capacity / grow_capacity_sum)
                };
                lengths[index] = (info.preferred + growth).min(info.max);
            }
        }
    }

    let mut cursor = 0.0;
    let mut slots = Vec::with_capacity(item_count);
    for (index, info) in infos.iter().enumerate() {
        cursor += info.margin_leading;
        let length = lengths[index].max(0.0);
        slots.push(LinearLayoutSlot {
            offset: cursor,
            length,
        });
        cursor += length + info.margin_trailing;
        if index + 1 < item_count {
            cursor += spacing;
        }
    }

    slots
}

fn sanitise_dimension(value: f64) -> f64 {
    if value.is_nan() {
        0.0
    } else if value.is_sign_negative() {
        0.0
    } else {
        value
    }
}

fn sanitise_weight(value: f64) -> f64 {
    if value.is_nan() || value <= 0.0 || !value.is_finite() {
        1.0
    } else {
        value
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::constraints::ScalarConstraint;

    #[test]
    fn distributes_space_using_weights() {
        let items = vec![
            LinearLayoutItem::new(ScalarConstraint::new(10.0, 20.0, 80.0)).with_weight(1.0),
            LinearLayoutItem::new(ScalarConstraint::new(10.0, 20.0, 80.0)).with_weight(2.0),
        ];

        let slots = solve_linear_layout(&items, 90.0, 0.0);
        assert_eq!(slots.len(), 2);

        let total: f64 = slots.iter().map(|slot| slot.length).sum();
        assert!((total - 90.0).abs() < 1e-6, "total length {total}");
        assert!((slots[0].offset).abs() < 1e-6);
        assert!((slots[0].length - 36.666).abs() < 1e-3);
        assert!((slots[1].offset - slots[0].length).abs() < 1e-6);
        assert!((slots[1].length - 53.333).abs() < 1e-3);
    }

    #[test]
    fn applies_spacing_between_items() {
        let items = vec![
            LinearLayoutItem::new(ScalarConstraint::tight(10.0)),
            LinearLayoutItem::new(ScalarConstraint::tight(10.0)),
        ];

        let slots = solve_linear_layout(&items, 50.0, 4.0);
        assert_eq!(slots.len(), 2);
        assert!((slots[0].offset - 0.0).abs() < 1e-6);
        assert!((slots[0].length - 10.0).abs() < 1e-6);
        assert!((slots[1].offset - 14.0).abs() < 1e-6);
        assert!((slots[1].length - 10.0).abs() < 1e-6);
    }
}

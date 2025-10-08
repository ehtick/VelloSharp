use std::collections::HashSet;

use vello::Scene;

#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct DirtyRegion {
    pub min_x: f64,
    pub max_x: f64,
    pub min_y: f64,
    pub max_y: f64,
}

impl DirtyRegion {
    pub fn new(x: f64, y: f64) -> Self {
        let x = sanitise_dimension(x);
        let y = sanitise_dimension(y);
        Self {
            min_x: x,
            max_x: x,
            min_y: y,
            max_y: y,
        }
    }

    pub fn expand(&mut self, x: f64, y: f64) {
        let x = sanitise_dimension(x);
        let y = sanitise_dimension(y);
        self.min_x = self.min_x.min(x);
        self.max_x = self.max_x.max(x);
        self.min_y = self.min_y.min(y);
        self.max_y = self.max_y.max(y);
    }

    pub fn merge(&mut self, other: &DirtyRegion) {
        self.min_x = self.min_x.min(other.min_x);
        self.max_x = self.max_x.max(other.max_x);
        self.min_y = self.min_y.min(other.min_y);
        self.max_y = self.max_y.max(other.max_y);
    }

    pub fn is_empty(&self) -> bool {
        !(self.min_x <= self.max_x && self.min_y <= self.max_y)
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct SceneNodeId(pub(crate) usize);

impl SceneNodeId {
    #[inline]
    pub fn index(self) -> usize {
        self.0
    }
}

struct SceneNode {
    parent: Option<SceneNodeId>,
    children: Vec<SceneNodeId>,
    dirty: Option<DirtyRegion>,
    scene: Scene,
}

impl SceneNode {
    fn new(parent: Option<SceneNodeId>) -> Self {
        Self {
            parent,
            children: Vec::new(),
            dirty: None,
            scene: Scene::new(),
        }
    }
}

pub struct SceneGraphCache {
    nodes: Vec<SceneNode>,
    reusable_nodes: HashSet<usize>,
}

impl SceneGraphCache {
    pub fn new() -> Self {
        Self {
            nodes: Vec::new(),
            reusable_nodes: HashSet::new(),
        }
    }

    pub fn capacity(&self) -> usize {
        self.nodes.len()
    }

    pub fn create_node(&mut self, parent: Option<SceneNodeId>) -> SceneNodeId {
        if let Some(index) = self.reusable_nodes.iter().copied().next() {
            self.reusable_nodes.remove(&index);
            let node_id = SceneNodeId(index);
            let node = &mut self.nodes[index];
            node.parent = parent;
            node.children.clear();
            node.dirty = None;
            node.scene.reset();
            if let Some(parent_id) = parent {
                self.attach_child(parent_id, node_id);
            }
            return node_id;
        }

        let node_id = SceneNodeId(self.nodes.len());
        let node = SceneNode::new(parent);
        self.nodes.push(node);
        if let Some(parent_id) = parent {
            self.attach_child(parent_id, node_id);
        }
        node_id
    }

    pub fn dispose_node(&mut self, node: SceneNodeId) {
        let parent_id = self.nodes.get(node.0).and_then(|node| node.parent);
        if let Some(parent_id) = parent_id {
            if let Some(parent) = self.nodes.get_mut(parent_id.0) {
                parent.children.retain(|child| child.0 != node.0);
            }
        }

        if let Some(entry) = self.nodes.get_mut(node.0) {
            entry.parent = None;
            entry.children.clear();
            entry.dirty = None;
            entry.scene.reset();
            self.reusable_nodes.insert(node.0);
        }
    }

    pub fn scene_mut(&mut self, node: SceneNodeId) -> Option<&mut Scene> {
        self.nodes.get_mut(node.0).map(|node| &mut node.scene)
    }

    pub fn scene(&self, node: SceneNodeId) -> Option<&Scene> {
        self.nodes.get(node.0).map(|node| &node.scene)
    }

    pub fn scene_mut_by_index(&mut self, index: usize) -> Option<&mut Scene> {
        self.nodes.get_mut(index).map(|node| &mut node.scene)
    }

    pub fn scene_by_index(&self, index: usize) -> Option<&Scene> {
        self.nodes.get(index).map(|node| &node.scene)
    }

    pub fn mark_dirty(&mut self, node: SceneNodeId, x: f64, y: f64) {
        if let Some(entry) = self.nodes.get_mut(node.0) {
            match &mut entry.dirty {
                Some(region) => region.expand(x, y),
                None => entry.dirty = Some(DirtyRegion::new(x, y)),
            }
        }
    }

    pub fn mark_dirty_bounds(
        &mut self,
        node: SceneNodeId,
        min_x: f64,
        max_x: f64,
        min_y: f64,
        max_y: f64,
    ) {
        let min_x = sanitise_dimension(min_x);
        let max_x = sanitise_dimension(max_x);
        let min_y = sanitise_dimension(min_y);
        let max_y = sanitise_dimension(max_y);
        let (x0, x1) = if min_x <= max_x {
            (min_x, max_x)
        } else {
            (max_x, min_x)
        };
        let (y0, y1) = if min_y <= max_y {
            (min_y, max_y)
        } else {
            (max_y, min_y)
        };

        if let Some(entry) = self.nodes.get_mut(node.0) {
            let dirty = entry.dirty.get_or_insert_with(|| DirtyRegion::new(x0, y0));
            dirty.expand(x0, y0);
            dirty.expand(x1, y1);
        }
    }

    pub fn take_dirty_recursive(&mut self, node: SceneNodeId) -> Option<DirtyRegion> {
        let mut accumulation = None;
        if let Some(entry) = self.nodes.get_mut(node.0) {
            if let Some(region) = entry.dirty.take() {
                accumulation = Some(region);
            }

            let children = entry.children.clone();

            for child in children {
                if let Some(child_region) = self.take_dirty_recursive(child) {
                    accumulation = Some(match accumulation {
                        Some(mut region) => {
                            region.merge(&child_region);
                            region
                        }
                        None => child_region,
                    });
                }
            }
        }
        accumulation
    }

    pub fn clear(&mut self, node: SceneNodeId) {
        if let Some(entry) = self.nodes.get_mut(node.0) {
            entry.dirty = None;
        }
    }

    fn attach_child(&mut self, parent_id: SceneNodeId, child_id: SceneNodeId) {
        if let Some(parent) = self.nodes.get_mut(parent_id.0) {
            if !parent.children.iter().any(|child| child.0 == child_id.0) {
                parent.children.push(child_id);
            }
        }
    }
}

fn sanitise_dimension(value: f64) -> f64 {
    if value.is_nan() {
        0.0
    } else if !value.is_finite() {
        if value.is_sign_negative() {
            0.0
        } else {
            f64::INFINITY
        }
    } else {
        value
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn aggregates_dirty_from_children() {
        let mut cache = SceneGraphCache::new();
        let root = cache.create_node(None);
        let child = cache.create_node(Some(root));

        cache.mark_dirty(child, 1.0, 2.0);
        cache.mark_dirty(child, 3.0, -1.0);

        let region = cache.take_dirty_recursive(root).expect("dirty region");
        assert!((region.min_x - 1.0).abs() < 1e-6);
        assert!((region.max_x - 3.0).abs() < 1e-6);
        assert!((region.min_y + 1.0).abs() < 1e-6);
        assert!((region.max_y - 2.0).abs() < 1e-6);

        assert!(cache.take_dirty_recursive(root).is_none());
    }

    #[test]
    fn mark_dirty_bounds_expands_region() {
        let mut cache = SceneGraphCache::new();
        let root = cache.create_node(None);

        cache.mark_dirty_bounds(root, 5.0, 15.0, -3.0, 9.0);
        cache.mark_dirty_bounds(root, 2.0, 4.0, -6.0, -1.0);

        let region = cache
            .take_dirty_recursive(root)
            .expect("dirty region should exist");
        assert!((region.min_x - 2.0).abs() < 1e-6);
        assert!((region.max_x - 15.0).abs() < 1e-6);
        assert!((region.min_y + 6.0).abs() < 1e-6);
        assert!((region.max_y - 9.0).abs() < 1e-6);
    }
}

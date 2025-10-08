use std::collections::VecDeque;

use hashbrown::{HashMap, HashSet, hash_map::Entry};

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct NodeId(pub u32);

impl NodeId {
    #[inline]
    pub fn index(self) -> usize {
        self.0 as usize
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum RowKind {
    Data,
    GroupHeader,
    Summary,
}

impl Default for RowKind {
    fn default() -> Self {
        RowKind::Data
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum SelectionMode {
    Replace,
    Add,
    Toggle,
    Range,
}

#[derive(Clone, Copy, Debug)]
pub struct NodeDescriptor {
    pub key: u64,
    pub row_kind: RowKind,
    pub height: f32,
    pub has_children: bool,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct NodeMetadata {
    pub key: u64,
    pub depth: u32,
    pub height: f32,
    pub row_kind: RowKind,
    pub is_expanded: bool,
    pub is_selected: bool,
    pub has_children: bool,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ModelDiffKind {
    Inserted,
    Removed,
    Expanded,
    Collapsed,
}

#[derive(Debug, Clone)]
pub struct ModelDiff {
    pub kind: ModelDiffKind,
    pub node_id: NodeId,
    pub parent_id: Option<NodeId>,
    pub index: u32,
    pub depth: u32,
    pub row_kind: RowKind,
    pub height: f32,
    pub has_children: bool,
    pub is_expanded: bool,
    pub key: u64,
}

#[derive(Debug, Clone)]
pub struct SelectionDiff {
    pub node_id: NodeId,
    pub is_selected: bool,
}

#[derive(Debug)]
struct Node {
    key: u64,
    parent: Option<NodeId>,
    children: Vec<NodeId>,
    depth: u32,
    index: u32,
    row_kind: RowKind,
    height: f32,
    has_children: bool,
    materialized: bool,
    is_expanded: bool,
    is_selected: bool,
}

impl Node {
    fn metadata(&self) -> NodeMetadata {
        NodeMetadata {
            key: self.key,
            depth: self.depth,
            height: self.height,
            row_kind: self.row_kind,
            is_expanded: self.is_expanded,
            is_selected: self.is_selected,
            has_children: self.has_children,
        }
    }
}

#[derive(Debug)]
pub enum ModelError {
    NodeNotFound,
    DuplicateKey(u64),
}

impl ModelError {
    pub fn message(&self) -> String {
        match self {
            ModelError::NodeNotFound => "TreeDataModel node not found".to_owned(),
            ModelError::DuplicateKey(key) => format!("TreeDataModel duplicate node key {key}"),
        }
    }
}

pub struct TreeDataModel {
    nodes: Vec<Option<Node>>,
    free_list: Vec<usize>,
    root_order: Vec<NodeId>,
    key_index: HashMap<u64, NodeId>,
    pending_materialization: VecDeque<NodeId>,
    pending_lookup: HashSet<NodeId>,
    model_diffs: Vec<ModelDiff>,
    selection_diffs: Vec<SelectionDiff>,
    selection_set: HashSet<NodeId>,
    selection_anchor: Option<NodeId>,
}

impl Default for TreeDataModel {
    fn default() -> Self {
        Self::new()
    }
}

impl TreeDataModel {
    pub fn new() -> Self {
        Self {
            nodes: Vec::new(),
            free_list: Vec::new(),
            root_order: Vec::new(),
            key_index: HashMap::new(),
            pending_materialization: VecDeque::new(),
            pending_lookup: HashSet::new(),
            model_diffs: Vec::new(),
            selection_diffs: Vec::new(),
            selection_set: HashSet::new(),
            selection_anchor: None,
        }
    }

    pub fn clear(&mut self) {
        self.nodes.clear();
        self.free_list.clear();
        self.root_order.clear();
        self.key_index.clear();
        self.pending_materialization.clear();
        self.pending_lookup.clear();
        self.model_diffs.clear();
        self.selection_diffs.clear();
        self.selection_set.clear();
        self.selection_anchor = None;
    }

    pub fn attach_roots(&mut self, descriptors: &[NodeDescriptor]) -> Result<(), ModelError> {
        self.attach_children_internal(None, descriptors)
    }

    pub fn attach_children(
        &mut self,
        parent_id: NodeId,
        descriptors: &[NodeDescriptor],
    ) -> Result<(), ModelError> {
        self.attach_children_internal(Some(parent_id), descriptors)
    }

    pub fn set_expanded(&mut self, node_id: NodeId, expanded: bool) -> Result<bool, ModelError> {
        let Some(node) = self.node_mut(node_id) else {
            return Err(ModelError::NodeNotFound);
        };

        if node.is_expanded == expanded {
            return Ok(false);
        }

        let parent = node.parent;
        let index = node.index;
        let depth = node.depth;
        let row_kind = node.row_kind;
        let height = node.height;
        let has_children = node.has_children;
        let key = node.key;
        let materialized = node.materialized;

        node.is_expanded = expanded;
        let kind = if expanded {
            ModelDiffKind::Expanded
        } else {
            ModelDiffKind::Collapsed
        };
        self.model_diffs.push(ModelDiff {
            kind,
            node_id,
            parent_id: parent,
            index,
            depth,
            row_kind,
            height,
            has_children,
            is_expanded: expanded,
            key,
        });

        if expanded && has_children && !materialized {
            self.enqueue_materialization(node_id);
        }

        Ok(true)
    }

    pub fn set_selected(&mut self, node_id: NodeId, mode: SelectionMode) -> Result<(), ModelError> {
        match mode {
            SelectionMode::Replace => {
                self.clear_selection();
                self.selection_anchor = Some(node_id);
                self.apply_selection(node_id, true)?;
            }
            SelectionMode::Add => {
                self.selection_anchor = Some(node_id);
                self.apply_selection(node_id, true)?;
            }
            SelectionMode::Toggle => {
                self.selection_anchor = Some(node_id);
                let currently_selected = self.selection_set.contains(&node_id);
                self.apply_selection(node_id, !currently_selected)?;
            }
            SelectionMode::Range => {
                let anchor = self.selection_anchor.unwrap_or(node_id);
                self.select_range(anchor, node_id)?;
            }
        }

        Ok(())
    }

    pub fn model_diffs(&self) -> &[ModelDiff] {
        &self.model_diffs
    }

    pub fn drain_model_diffs(&mut self, count: usize) {
        let remove = count.min(self.model_diffs.len());
        self.model_diffs.drain(0..remove);
    }

    pub fn clear_model_diffs(&mut self) {
        self.model_diffs.clear();
    }

    pub fn selection_diffs(&self) -> &[SelectionDiff] {
        &self.selection_diffs
    }

    pub fn drain_selection_diffs(&mut self, count: usize) {
        let remove = count.min(self.selection_diffs.len());
        self.selection_diffs.drain(0..remove);
    }

    pub fn clear_selection_diffs(&mut self) {
        self.selection_diffs.clear();
    }

    pub fn dequeue_materialization(&mut self) -> Option<NodeId> {
        while let Some(node_id) = self.pending_materialization.pop_front() {
            if !self.pending_lookup.remove(&node_id) {
                continue;
            }

            if let Some(node) = self.node(node_id) {
                if node.has_children && !node.materialized {
                    return Some(node_id);
                }
            }
        }
        None
    }

    pub fn node_metadata(&self, node_id: NodeId) -> Option<NodeMetadata> {
        self.node(node_id).map(Node::metadata)
    }

    fn attach_children_internal(
        &mut self,
        parent_id: Option<NodeId>,
        descriptors: &[NodeDescriptor],
    ) -> Result<(), ModelError> {
        self.ensure_unique_keys(descriptors)?;

        let (existing_children, depth) = if let Some(parent) = parent_id {
            let Some(parent_node) = self.node(parent) else {
                return Err(ModelError::NodeNotFound);
            };
            (parent_node.children.clone(), parent_node.depth + 1)
        } else {
            (self.root_order.clone(), 0)
        };

        let mut existing_map = HashMap::new();
        for child_id in &existing_children {
            if let Some(node) = self.node(*child_id) {
                existing_map.insert(node.key, *child_id);
            }
        }

        let mut reused: HashSet<NodeId> = HashSet::new();
        let mut prune_targets: Vec<NodeId> = Vec::new();
        let mut new_children: Vec<NodeId> = Vec::with_capacity(descriptors.len());

        for (index, descriptor) in descriptors.iter().enumerate() {
            let child_index = index as u32;
            match existing_map.entry(descriptor.key) {
                Entry::Occupied(entry) => {
                    let child_id = *entry.get();
                    if let Some(node) = self.node_mut(child_id) {
                        node.parent = parent_id;
                        node.depth = depth;
                        node.index = child_index;
                        node.row_kind = descriptor.row_kind;
                        node.height = descriptor.height;
                        node.has_children = descriptor.has_children;
                        if !descriptor.has_children {
                            node.materialized = true;
                            prune_targets.push(child_id);
                        }
                        reused.insert(child_id);
                        new_children.push(child_id);
                    }
                }
                Entry::Vacant(_) => {
                    let node_id = self.allocate_node(*descriptor, parent_id, depth, child_index);
                    new_children.push(node_id);
                    self.model_diffs.push(ModelDiff {
                        kind: ModelDiffKind::Inserted,
                        node_id,
                        parent_id,
                        index: child_index,
                        depth,
                        row_kind: descriptor.row_kind,
                        height: descriptor.height,
                        has_children: descriptor.has_children,
                        is_expanded: false,
                        key: descriptor.key,
                    });
                }
            }
        }

        for child in prune_targets {
            self.prune_all_children(child);
        }

        // Record removals for nodes no longer present.
        for (old_index, child_id) in existing_children.into_iter().enumerate() {
            if reused.contains(&child_id) {
                continue;
            }

            if let Some(node) = self.node(child_id) {
                self.model_diffs.push(ModelDiff {
                    kind: ModelDiffKind::Removed,
                    node_id: child_id,
                    parent_id: node.parent,
                    index: old_index as u32,
                    depth: node.depth,
                    row_kind: node.row_kind,
                    height: node.height,
                    has_children: node.has_children,
                    is_expanded: node.is_expanded,
                    key: node.key,
                });
            }
            self.prune_subtree(child_id);
        }

        if let Some(parent) = parent_id {
            if let Some(node) = self.node_mut(parent) {
                node.children = new_children.clone();
                node.materialized = true;
                if !node.has_children {
                    node.has_children = !node.children.is_empty();
                }
            }
        } else {
            self.root_order = new_children;
        }

        Ok(())
    }

    fn ensure_unique_keys(&mut self, descriptors: &[NodeDescriptor]) -> Result<(), ModelError> {
        let mut seen = HashSet::new();
        for descriptor in descriptors {
            if !seen.insert(descriptor.key) {
                return Err(ModelError::DuplicateKey(descriptor.key));
            }
        }
        Ok(())
    }

    fn allocate_node(
        &mut self,
        descriptor: NodeDescriptor,
        parent: Option<NodeId>,
        depth: u32,
        index: u32,
    ) -> NodeId {
        let slot = self.free_list.pop().unwrap_or_else(|| {
            self.nodes.push(None);
            self.nodes.len() - 1
        });
        let node_id = NodeId(slot as u32);
        let node = Node {
            key: descriptor.key,
            parent,
            children: Vec::new(),
            depth,
            index,
            row_kind: descriptor.row_kind,
            height: descriptor.height,
            has_children: descriptor.has_children,
            materialized: !descriptor.has_children,
            is_expanded: false,
            is_selected: false,
        };
        if let Some(existing) = self.key_index.insert(descriptor.key, node_id) {
            // Removing previous node with the same key to avoid duplicates.
            self.prune_subtree(existing);
        }
        self.nodes[slot] = Some(node);
        node_id
    }

    fn prune_all_children(&mut self, node_id: NodeId) {
        let Some(children) = self.node(node_id).map(|node| node.children.clone()) else {
            return;
        };
        for child in children {
            self.prune_subtree(child);
        }
        if let Some(node) = self.node_mut(node_id) {
            node.children.clear();
        }
    }

    fn prune_subtree(&mut self, node_id: NodeId) {
        let mut stack = vec![node_id];
        while let Some(current) = stack.pop() {
            let entry = current.index();
            if let Some(mut node) = self.nodes.get_mut(entry).and_then(Option::take) {
                self.key_index.remove(&node.key);
                if node.is_selected {
                    self.selection_set.remove(&current);
                    self.selection_diffs.push(SelectionDiff {
                        node_id: current,
                        is_selected: false,
                    });
                }
                self.pending_lookup.remove(&current);
                stack.extend(node.children.iter().copied());
                node.children.clear();
                self.free_list.push(entry);
            }
        }
    }

    fn node(&self, node_id: NodeId) -> Option<&Node> {
        self.nodes
            .get(node_id.index())
            .and_then(|slot| slot.as_ref())
    }

    fn node_mut(&mut self, node_id: NodeId) -> Option<&mut Node> {
        self.nodes
            .get_mut(node_id.index())
            .and_then(|slot| slot.as_mut())
    }

    fn enqueue_materialization(&mut self, node_id: NodeId) {
        if self.pending_lookup.insert(node_id) {
            self.pending_materialization.push_back(node_id);
        }
    }

    fn apply_selection(&mut self, node_id: NodeId, selected: bool) -> Result<(), ModelError> {
        let Some(node) = self.node_mut(node_id) else {
            return Err(ModelError::NodeNotFound);
        };

        if node.is_selected == selected {
            return Ok(());
        }

        node.is_selected = selected;
        if selected {
            self.selection_set.insert(node_id);
        } else {
            self.selection_set.remove(&node_id);
        }

        self.selection_diffs.push(SelectionDiff {
            node_id,
            is_selected: selected,
        });

        Ok(())
    }

    fn clear_selection(&mut self) {
        if self.selection_set.is_empty() {
            return;
        }
        let drained: Vec<NodeId> = self.selection_set.drain().collect();
        for node_id in drained {
            if let Some(node) = self.node_mut(node_id) {
                node.is_selected = false;
            }
            self.selection_diffs.push(SelectionDiff {
                node_id,
                is_selected: false,
            });
        }
    }

    fn select_range(&mut self, anchor: NodeId, focus: NodeId) -> Result<(), ModelError> {
        let order = self.visible_order();
        let Some(anchor_index) = order.iter().position(|id| *id == anchor) else {
            return Err(ModelError::NodeNotFound);
        };
        let Some(focus_index) = order.iter().position(|id| *id == focus) else {
            return Err(ModelError::NodeNotFound);
        };

        let (start, end) = if anchor_index <= focus_index {
            (anchor_index, focus_index)
        } else {
            (focus_index, anchor_index)
        };

        let mut range_set = HashSet::with_capacity(end - start + 1);
        for node_id in &order[start..=end] {
            range_set.insert(*node_id);
        }

        let existing: Vec<NodeId> = self.selection_set.iter().copied().collect();
        for node_id in existing {
            if !range_set.contains(&node_id) {
                self.apply_selection(node_id, false)?;
            }
        }

        for node_id in range_set {
            self.apply_selection(node_id, true)?;
        }

        Ok(())
    }

    fn visible_order(&self) -> Vec<NodeId> {
        let mut order = Vec::new();
        for root in &self.root_order {
            self.collect_visible(*root, &mut order);
        }
        order
    }

    fn collect_visible(&self, node_id: NodeId, order: &mut Vec<NodeId>) {
        let Some(node) = self.node(node_id) else {
            return;
        };
        order.push(node_id);
        if node.is_expanded {
            for child in &node.children {
                self.collect_visible(*child, order);
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn new_descriptor(key: u64, has_children: bool) -> NodeDescriptor {
        NodeDescriptor {
            key,
            row_kind: RowKind::Data,
            height: 24.0,
            has_children,
        }
    }

    #[test]
    fn attach_roots_produces_insert_diffs() {
        let mut model = TreeDataModel::new();
        let nodes = [new_descriptor(1, true), new_descriptor(2, false)];
        model.attach_roots(&nodes).expect("attach roots");

        let diffs = model.model_diffs();
        assert_eq!(diffs.len(), 2);
        assert!(
            diffs
                .iter()
                .any(|diff| diff.key == 1 && diff.kind == ModelDiffKind::Inserted)
        );
        assert!(
            diffs
                .iter()
                .any(|diff| diff.key == 2 && diff.kind == ModelDiffKind::Inserted)
        );
    }

    #[test]
    fn expanding_node_enqueues_materialization() {
        let mut model = TreeDataModel::new();
        model
            .attach_roots(&[new_descriptor(1, true)])
            .expect("attach roots");

        let node_id = model
            .model_diffs()
            .first()
            .map(|diff| diff.node_id)
            .expect("diff");

        model.clear_model_diffs();
        let expanded = model.set_expanded(node_id, true).expect("set expanded");
        assert!(expanded);
        assert!(model.dequeue_materialization().is_some());
    }

    #[test]
    fn selection_range_clears_previous_selection() {
        let mut model = TreeDataModel::new();
        let roots = [
            new_descriptor(1, false),
            new_descriptor(2, false),
            new_descriptor(3, false),
        ];
        model.attach_roots(&roots).expect("attach roots");
        let ids: Vec<NodeId> = model
            .model_diffs()
            .iter()
            .map(|diff| diff.node_id)
            .collect();
        model.clear_model_diffs();

        model
            .set_selected(ids[0], SelectionMode::Replace)
            .expect("select replace");
        model
            .set_selected(ids[2], SelectionMode::Range)
            .expect("select range");

        let selected: Vec<_> = model.selection_diffs().iter().collect();
        assert!(selected.iter().any(|diff| diff.is_selected));
        assert!(model.selection_diffs().len() >= 2);
    }
}

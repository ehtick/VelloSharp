use crate::scene_cache::{SceneGraphCache, SceneNodeId};

const EPSILON_F32: f32 = 1e-6;
const EPSILON_F64: f64 = 1e-9;

pub type TimelineGroupId = u32;
pub type TimelineTrackId = u32;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum RepeatMode {
    Once,
    Loop,
    PingPong,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum EasingFunction {
    Linear,
    EaseInQuad,
    EaseOutQuad,
    EaseInOutQuad,
    EaseInCubic,
    EaseOutCubic,
    EaseInOutCubic,
    EaseInQuart,
    EaseOutQuart,
    EaseInOutQuart,
    EaseInQuint,
    EaseOutQuint,
    EaseInOutQuint,
    EaseInSine,
    EaseOutSine,
    EaseInOutSine,
    EaseInExpo,
    EaseOutExpo,
    EaseInOutExpo,
    EaseInCirc,
    EaseOutCirc,
    EaseInOutCirc,
}

#[derive(Clone, Copy, Debug)]
pub enum DirtyIntent {
    None,
    Point {
        x: f64,
        y: f64,
    },
    Bounds {
        min_x: f64,
        max_x: f64,
        min_y: f64,
        max_y: f64,
    },
}

#[derive(Clone, Copy, Debug)]
pub struct TimelineSample {
    pub track_id: TimelineTrackId,
    pub node_id: SceneNodeId,
    pub channel_id: u16,
    pub flags: u16,
    pub value: f32,
    pub velocity: f32,
    pub progress: f32,
}

pub const SAMPLE_FLAG_ACTIVE: u16 = 1 << 0;
pub const SAMPLE_FLAG_COMPLETED: u16 = 1 << 1;
pub const SAMPLE_FLAG_LOOPED: u16 = 1 << 2;
pub const SAMPLE_FLAG_PINGPONG_REVERSED: u16 = 1 << 3;
pub const SAMPLE_FLAG_AT_REST: u16 = 1 << 4;

#[derive(Clone, Copy, Debug)]
pub struct TimelineGroupConfig {
    pub speed: f32,
    pub autoplay: bool,
}

impl Default for TimelineGroupConfig {
    fn default() -> Self {
        Self {
            speed: 1.0,
            autoplay: true,
        }
    }
}

#[derive(Clone, Copy, Debug)]
pub struct EasingTrackDescriptor {
    pub node_id: SceneNodeId,
    pub channel_id: u16,
    pub repeat: RepeatMode,
    pub easing: EasingFunction,
    pub start_value: f32,
    pub end_value: f32,
    pub duration: f32,
    pub dirty_intent: DirtyIntent,
}

#[derive(Clone, Copy, Debug)]
pub struct SpringTrackDescriptor {
    pub node_id: SceneNodeId,
    pub channel_id: u16,
    pub stiffness: f32,
    pub damping: f32,
    pub mass: f32,
    pub start_value: f32,
    pub initial_velocity: f32,
    pub target_value: f32,
    pub rest_velocity: f32,
    pub rest_offset: f32,
    pub dirty_intent: DirtyIntent,
}

pub struct TimelineSystem {
    groups: Vec<Option<TimelineGroup>>,
    tracks: Vec<Option<TimelineTrack>>,
    free_groups: Vec<usize>,
    free_tracks: Vec<usize>,
    samples: Vec<TimelineSample>,
}

impl TimelineSystem {
    pub fn new() -> Self {
        Self {
            groups: Vec::new(),
            tracks: Vec::new(),
            free_groups: Vec::new(),
            free_tracks: Vec::new(),
            samples: Vec::new(),
        }
    }

    pub fn create_group(&mut self, config: TimelineGroupConfig) -> TimelineGroupId {
        let group = TimelineGroup {
            playing: config.autoplay,
            speed: config.speed.max(0.0),
        };

        if let Some(index) = self.free_groups.pop() {
            self.groups[index] = Some(group);
            return index as TimelineGroupId;
        }

        let index = self.groups.len();
        self.groups.push(Some(group));
        index as TimelineGroupId
    }

    pub fn destroy_group(&mut self, group_id: TimelineGroupId) {
        let index = group_id as usize;
        if index >= self.groups.len() || self.groups[index].is_none() {
            return;
        }

        self.groups[index] = None;
        self.free_groups.push(index);
    }

    pub fn set_group_playing(&mut self, group_id: TimelineGroupId, playing: bool) {
        if let Some(group) = self.group_mut(group_id) {
            group.playing = playing;
        }
    }

    pub fn set_group_speed(&mut self, group_id: TimelineGroupId, speed: f32) {
        if let Some(group) = self.group_mut(group_id) {
            group.speed = speed.max(0.0);
        }
    }

    pub fn add_easing_track(
        &mut self,
        group_id: TimelineGroupId,
        descriptor: EasingTrackDescriptor,
    ) -> Option<TimelineTrackId> {
        let Some(_) = self.group(group_id) else {
            return None;
        };

        if descriptor.duration <= EPSILON_F32 {
            return None;
        }

        let track = TimelineTrack {
            group: group_id,
            channel_id: descriptor.channel_id,
            target: descriptor.node_id,
            dirty_intent: descriptor.dirty_intent,
            repeat: descriptor.repeat,
            mode: TrackMode::Easing(EasingTrack {
                start_value: descriptor.start_value,
                end_value: descriptor.end_value,
                duration: descriptor.duration.max(EPSILON_F32),
                easing: descriptor.easing,
            }),
            state: TrackState {
                elapsed: 0.0,
                direction: Direction::Forward,
                value: descriptor.start_value,
                velocity: 0.0,
                progress: 0.0,
                active: true,
            },
        };

        Some(self.insert_track(track))
    }

    pub fn add_spring_track(
        &mut self,
        group_id: TimelineGroupId,
        descriptor: SpringTrackDescriptor,
    ) -> Option<TimelineTrackId> {
        let Some(_) = self.group(group_id) else {
            return None;
        };

        let stiffness = descriptor.stiffness.max(EPSILON_F32);
        let damping = descriptor.damping.max(0.0);
        let mass = descriptor.mass.max(EPSILON_F32);
        let rest_velocity = descriptor.rest_velocity.max(0.0);
        let rest_offset = descriptor.rest_offset.max(0.0);

        let track = TimelineTrack {
            group: group_id,
            channel_id: descriptor.channel_id,
            target: descriptor.node_id,
            dirty_intent: descriptor.dirty_intent,
            repeat: RepeatMode::Once,
            mode: TrackMode::Spring(SpringTrack {
                config: SpringConfig {
                    stiffness,
                    damping,
                    mass,
                    target: descriptor.target_value,
                    rest_velocity,
                    rest_offset,
                },
                state: SpringState {
                    position: descriptor.start_value,
                    velocity: descriptor.initial_velocity,
                },
            }),
            state: TrackState {
                elapsed: 0.0,
                direction: Direction::Forward,
                value: descriptor.start_value,
                velocity: descriptor.initial_velocity,
                progress: 0.0,
                active: true,
            },
        };

        Some(self.insert_track(track))
    }

    pub fn set_spring_target(&mut self, track_id: TimelineTrackId, target: f32) {
        if let Some(track) = self.track_mut(track_id) {
            if let TrackMode::Spring(ref mut spring) = track.mode {
                spring.config.target = target;
                track.state.active = true;
            }
        }
    }

    pub fn reset_track(&mut self, track_id: TimelineTrackId) {
        if let Some(track) = self.track_mut(track_id) {
            track.reset_state();
        }
    }

    pub fn remove_track(&mut self, track_id: TimelineTrackId) {
        let index = track_id as usize;
        if index >= self.tracks.len() {
            return;
        }

        if self.tracks[index].is_some() {
            self.tracks[index] = None;
            self.free_tracks.push(index);
        }
    }

    pub fn tick<'a>(
        &'a mut self,
        delta_seconds: f64,
        cache: Option<&mut SceneGraphCache>,
    ) -> &'a [TimelineSample] {
        self.samples.clear();

        if delta_seconds <= EPSILON_F64 {
            return &self.samples;
        }

        // Split cache reference so we can pass mutable borrow to tracks.
        let mut cache_option = cache;

        for index in 0..self.tracks.len() {
            let Some(track_ref) = self.tracks[index].as_ref() else {
                continue;
            };

            let Some(group_ref) = self
                .groups
                .get(track_ref.group as usize)
                .and_then(|group| group.as_ref())
            else {
                continue;
            };

            if !group_ref.playing || !track_ref.state.active {
                continue;
            };

            let speed = group_ref.speed;
            let scaled_dt = delta_seconds * speed as f64;
            if scaled_dt.abs() <= EPSILON_F64 {
                continue;
            }

            let Some(track) = self.tracks[index].as_mut() else {
                continue;
            };

            let sample = track.tick(
                scaled_dt,
                delta_seconds,
                index as TimelineTrackId,
                cache_option.as_mut().map(|cache| &mut **cache),
            );

            if let Some(sample) = sample {
                self.samples.push(sample);
            }
        }

        &self.samples
    }

    fn group(&self, group_id: TimelineGroupId) -> Option<&TimelineGroup> {
        self.groups
            .get(group_id as usize)
            .and_then(|group| group.as_ref())
    }

    fn group_mut(&mut self, group_id: TimelineGroupId) -> Option<&mut TimelineGroup> {
        self.groups
            .get_mut(group_id as usize)
            .and_then(|group| group.as_mut())
    }

    fn track_mut(&mut self, track_id: TimelineTrackId) -> Option<&mut TimelineTrack> {
        self.tracks
            .get_mut(track_id as usize)
            .and_then(|track| track.as_mut())
    }

    fn insert_track(&mut self, track: TimelineTrack) -> TimelineTrackId {
        if let Some(index) = self.free_tracks.pop() {
            self.tracks[index] = Some(track);
            return index as TimelineTrackId;
        }

        let index = self.tracks.len();
        self.tracks.push(Some(track));
        index as TimelineTrackId
    }
}

struct TimelineGroup {
    playing: bool,
    speed: f32,
}

struct TimelineTrack {
    group: TimelineGroupId,
    channel_id: u16,
    target: SceneNodeId,
    dirty_intent: DirtyIntent,
    repeat: RepeatMode,
    mode: TrackMode,
    state: TrackState,
}

impl TimelineTrack {
    fn reset_state(&mut self) {
        match &mut self.mode {
            TrackMode::Easing(easing) => {
                self.state.elapsed = 0.0;
                self.state.direction = Direction::Forward;
                self.state.value = easing.start_value;
                self.state.velocity = 0.0;
                self.state.progress = 0.0;
                self.state.active = true;
            }
            TrackMode::Spring(spring) => {
                self.state.elapsed = 0.0;
                self.state.direction = Direction::Forward;
                self.state.value = spring.state.position;
                self.state.velocity = spring.state.velocity;
                self.state.progress = 0.0;
                self.state.active = true;
            }
        }
    }

    fn tick(
        &mut self,
        scaled_dt: f64,
        real_dt: f64,
        track_id: TimelineTrackId,
        cache: Option<&mut SceneGraphCache>,
    ) -> Option<TimelineSample> {
        let mut flags = SAMPLE_FLAG_ACTIVE;
        let mut looped = false;

        let previous_value = self.state.value;

        match &mut self.mode {
            TrackMode::Easing(easing) => {
                let duration = easing.duration.max(EPSILON_F32) as f64;
                self.state.elapsed += scaled_dt * self.state.direction.sign();

                if self.state.elapsed >= duration {
                    match self.repeat {
                        RepeatMode::Once => {
                            self.state.elapsed = duration;
                            self.state.active = false;
                            flags |= SAMPLE_FLAG_COMPLETED;
                        }
                        RepeatMode::Loop => {
                            let overflow = self.state.elapsed % duration;
                            self.state.elapsed = overflow;
                            looped = true;
                        }
                        RepeatMode::PingPong => {
                            let overflow = self.state.elapsed - duration;
                            self.state.elapsed = duration - overflow;
                            self.state.direction = Direction::Reverse;
                            looped = true;
                        }
                    }
                } else if self.state.elapsed <= 0.0 {
                    if self.repeat == RepeatMode::PingPong {
                        let overflow = -self.state.elapsed;
                        self.state.elapsed = overflow.min(duration);
                        self.state.direction = Direction::Forward;
                        looped = true;
                    } else {
                        self.state.elapsed = 0.0;
                        if matches!(self.repeat, RepeatMode::Once) {
                            self.state.active = false;
                            flags |= SAMPLE_FLAG_COMPLETED;
                        }
                    }
                }

                let raw_progress = (self.state.elapsed / duration) as f32;
                let clamped_raw = raw_progress.clamp(0.0, 1.0);
                let progress = if self.repeat == RepeatMode::PingPong
                    && self.state.direction == Direction::Reverse
                {
                    1.0 - clamped_raw
                } else {
                    clamped_raw
                };

                self.state.progress = progress;
                let eased = easing.easing.sample(progress);
                let delta = easing.end_value - easing.start_value;
                self.state.value = easing.start_value + delta * eased;
                if real_dt > 0.0 {
                    self.state.velocity = (self.state.value - previous_value) / real_dt as f32;
                } else {
                    self.state.velocity = 0.0;
                }
            }
            TrackMode::Spring(spring) => {
                let dt = scaled_dt as f32;
                let at_rest = spring.step(dt);
                self.state.value = spring.state.position;
                self.state.velocity = spring.state.velocity;
                self.state.progress = 1.0
                    - ((spring.state.position - spring.config.target).abs()
                        / spring.config.rest_offset.max(EPSILON_F32))
                    .clamp(0.0, 1.0);

                if at_rest {
                    flags |= SAMPLE_FLAG_AT_REST;
                    self.state.active = false;
                    flags |= SAMPLE_FLAG_COMPLETED;
                }
            }
        }

        if looped {
            flags |= SAMPLE_FLAG_LOOPED;
        }
        if self.repeat == RepeatMode::PingPong && matches!(self.state.direction, Direction::Reverse)
        {
            flags |= SAMPLE_FLAG_PINGPONG_REVERSED;
        }

        if (self.state.value - previous_value).abs() <= EPSILON_F32 && flags == SAMPLE_FLAG_ACTIVE {
            return None;
        }

        if let Some(cache) = cache {
            match self.dirty_intent {
                DirtyIntent::None => {}
                DirtyIntent::Point { x, y } => {
                    cache.mark_dirty(self.target, x, y);
                }
                DirtyIntent::Bounds {
                    min_x,
                    max_x,
                    min_y,
                    max_y,
                } => {
                    cache.mark_dirty_bounds(self.target, min_x, max_x, min_y, max_y);
                }
            }
        }

        Some(TimelineSample {
            track_id,
            node_id: self.target,
            channel_id: self.channel_id,
            flags,
            value: self.state.value,
            velocity: self.state.velocity,
            progress: self.state.progress,
        })
    }
}

enum TrackMode {
    Easing(EasingTrack),
    Spring(SpringTrack),
}

struct TrackState {
    elapsed: f64,
    direction: Direction,
    value: f32,
    velocity: f32,
    progress: f32,
    active: bool,
}

#[derive(Clone, Copy)]
struct EasingTrack {
    start_value: f32,
    end_value: f32,
    duration: f32,
    easing: EasingFunction,
}

struct SpringTrack {
    config: SpringConfig,
    state: SpringState,
}

impl SpringTrack {
    fn step(&mut self, dt: f32) -> bool {
        let dt = dt.max(EPSILON_F32);
        let config = &self.config;
        let state = &mut self.state;

        let inv_mass = 1.0 / config.mass;
        let displacement = state.position - config.target;
        let spring_force = -config.stiffness * displacement;
        let damping_force = -config.damping * state.velocity;
        let acceleration = (spring_force + damping_force) * inv_mass;

        state.velocity += acceleration * dt;
        state.position += state.velocity * dt;

        let velocity_abs = state.velocity.abs();
        let offset_abs = (state.position - config.target).abs();
        velocity_abs <= config.rest_velocity && offset_abs <= config.rest_offset
    }
}

struct SpringConfig {
    stiffness: f32,
    damping: f32,
    mass: f32,
    target: f32,
    rest_velocity: f32,
    rest_offset: f32,
}

struct SpringState {
    position: f32,
    velocity: f32,
}

#[derive(Clone, Copy, PartialEq, Eq)]
enum Direction {
    Forward,
    Reverse,
}

impl Direction {
    fn sign(self) -> f64 {
        match self {
            Direction::Forward => 1.0,
            Direction::Reverse => -1.0,
        }
    }
}

impl EasingFunction {
    fn sample(self, t: f32) -> f32 {
        let t = t.clamp(0.0, 1.0);
        match self {
            EasingFunction::Linear => t,
            EasingFunction::EaseInQuad => t * t,
            EasingFunction::EaseOutQuad => {
                let inv = 1.0 - t;
                1.0 - inv * inv
            }
            EasingFunction::EaseInOutQuad => {
                if t < 0.5 {
                    2.0 * t * t
                } else {
                    let inv = 1.0 - t;
                    1.0 - 2.0 * inv * inv
                }
            }
            EasingFunction::EaseInCubic => t * t * t,
            EasingFunction::EaseOutCubic => {
                let inv = 1.0 - t;
                1.0 - inv * inv * inv
            }
            EasingFunction::EaseInOutCubic => {
                if t < 0.5 {
                    4.0 * t * t * t
                } else {
                    let inv = 1.0 - t;
                    1.0 - 4.0 * inv * inv * inv
                }
            }
            EasingFunction::EaseInQuart => t * t * t * t,
            EasingFunction::EaseOutQuart => {
                let inv = 1.0 - t;
                1.0 - inv * inv * inv * inv
            }
            EasingFunction::EaseInOutQuart => {
                if t < 0.5 {
                    8.0 * t * t * t * t
                } else {
                    let inv = 1.0 - t;
                    1.0 - 8.0 * inv * inv * inv * inv
                }
            }
            EasingFunction::EaseInQuint => t * t * t * t * t,
            EasingFunction::EaseOutQuint => {
                let inv = 1.0 - t;
                1.0 - inv * inv * inv * inv * inv
            }
            EasingFunction::EaseInOutQuint => {
                if t < 0.5 {
                    16.0 * t * t * t * t * t
                } else {
                    let inv = 1.0 - t;
                    1.0 - 16.0 * inv * inv * inv * inv * inv
                }
            }
            EasingFunction::EaseInSine => 1.0 - (t * std::f32::consts::FRAC_PI_2).cos(),
            EasingFunction::EaseOutSine => (t * std::f32::consts::FRAC_PI_2).sin(),
            EasingFunction::EaseInOutSine => -(std::f32::consts::PI * (t - 0.5)).cos() * 0.5 + 0.5,
            EasingFunction::EaseInExpo => {
                if t <= 0.0 {
                    0.0
                } else {
                    (2.0_f32).powf(10.0 * (t - 1.0))
                }
            }
            EasingFunction::EaseOutExpo => {
                if t >= 1.0 {
                    1.0
                } else {
                    1.0 - (2.0_f32).powf(-10.0 * t)
                }
            }
            EasingFunction::EaseInOutExpo => {
                if t <= 0.0 {
                    0.0
                } else if t >= 1.0 {
                    1.0
                } else if t < 0.5 {
                    (2.0_f32).powf(10.0 * (2.0 * t - 1.0)) * 0.5
                } else {
                    (2.0_f32).powf(-10.0 * (2.0 * t - 1.0)) * 0.5 + 0.5
                }
            }
            EasingFunction::EaseInCirc => 1.0 - (1.0 - t * t).sqrt(),
            EasingFunction::EaseOutCirc => (1.0 - (t - 1.0) * (t - 1.0)).sqrt(),
            EasingFunction::EaseInOutCirc => {
                if t < 0.5 {
                    0.5 * (1.0 - (1.0 - 4.0 * t * t).sqrt())
                } else {
                    0.5 * ((-((2.0 * t - 1.0) * (2.0 * t - 1.0)) + 1.0).sqrt() + 1.0)
                }
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn easing_track_advances_and_marks_dirty() {
        let mut system = TimelineSystem::new();
        let group = system.create_group(TimelineGroupConfig::default());
        let mut cache = SceneGraphCache::new();
        let node = cache.create_node(None);

        let descriptor = EasingTrackDescriptor {
            node_id: node,
            channel_id: 1,
            repeat: RepeatMode::Once,
            easing: EasingFunction::Linear,
            start_value: 0.0,
            end_value: 10.0,
            duration: 1.0,
            dirty_intent: DirtyIntent::Bounds {
                min_x: 0.0,
                max_x: 10.0,
                min_y: 0.0,
                max_y: 10.0,
            },
        };

        let track_id = system
            .add_easing_track(group, descriptor)
            .expect("track id");

        let samples = system.tick(0.5, Some(&mut cache));
        assert!(!samples.is_empty());
        let sample = samples[0];
        assert_eq!(sample.track_id, track_id);
        assert!(sample.value > 4.9 && sample.value < 5.1);

        let region = cache
            .take_dirty_recursive(node)
            .expect("dirty region expected");
        assert!((region.min_x - 0.0).abs() < 1e-6);
        assert!((region.max_x - 10.0).abs() < 1e-6);

        let samples = system.tick(0.5, Some(&mut cache));
        assert!(!samples.is_empty());
        let sample = samples[0];
        assert_eq!(sample.flags & SAMPLE_FLAG_COMPLETED, SAMPLE_FLAG_COMPLETED);
    }

    #[test]
    fn spring_track_converges_to_target() {
        let mut system = TimelineSystem::new();
        let group = system.create_group(TimelineGroupConfig::default());
        let mut cache = SceneGraphCache::new();
        let node = cache.create_node(None);

        let descriptor = SpringTrackDescriptor {
            node_id: node,
            channel_id: 0,
            stiffness: 90.0,
            damping: 14.0,
            mass: 1.0,
            start_value: 0.0,
            initial_velocity: 0.0,
            target_value: 1.0,
            rest_velocity: 0.0005,
            rest_offset: 0.0005,
            dirty_intent: DirtyIntent::None,
        };

        system
            .add_spring_track(group, descriptor)
            .expect("track id");

        let mut value = 0.0;
        for _ in 0..120 {
            let samples = system.tick(1.0 / 120.0, Some(&mut cache));
            if let Some(sample) = samples.last() {
                value = sample.value;
                if sample.flags & SAMPLE_FLAG_COMPLETED == SAMPLE_FLAG_COMPLETED {
                    break;
                }
            }
        }

        assert!((value - 1.0).abs() < 0.01);
    }
}

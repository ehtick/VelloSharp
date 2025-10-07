//! Streaming data structures powering the chart engine.

use crossbeam_channel::{Receiver, Sender, TrySendError};
use serde::{Deserialize, Serialize};

/// A single data point emitted by a series.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DataPoint {
    pub timestamp_ns: u64,
    pub value: f64,
}

/// Batch of points for a single series.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SeriesSample {
    pub series_id: u32,
    pub points: Vec<DataPoint>,
}

/// Non-blocking data ingress channel for the chart engine.
#[derive(Debug, Clone)]
pub struct DataBus {
    sender: Sender<SeriesSample>,
    receiver: Receiver<SeriesSample>,
}

impl DataBus {
    pub fn new(capacity: usize) -> Self {
        let (sender, receiver) = crossbeam_channel::bounded(capacity);
        Self { sender, receiver }
    }

    pub fn push(&self, sample: SeriesSample) -> Result<(), TrySendError<SeriesSample>> {
        self.sender.try_send(sample)
    }

    pub fn drain(&self, output: &mut Vec<SeriesSample>) {
        while let Ok(sample) = self.receiver.try_recv() {
            output.push(sample);
        }
    }
}

#!/usr/bin/env node
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";

const MGMT_URL = process.env.RABBITMQ_MGMT_URL || "http://localhost:15672";
const USER = process.env.RABBITMQ_USER || "poc";
const PASSWORD = process.env.RABBITMQ_PASSWORD || "poc";
const VHOST = process.env.RABBITMQ_VHOST || "/";

const auth = Buffer.from(`${USER}:${PASSWORD}`).toString("base64");
const encodedVhost = encodeURIComponent(VHOST);

async function mgmt(path, method = "GET", body = null) {
  const opts = {
    method,
    headers: {
      Authorization: `Basic ${auth}`,
      "Content-Type": "application/json",
    },
  };
  if (body !== null) opts.body = JSON.stringify(body);
  const res = await fetch(`${MGMT_URL}/api${path}`, opts);
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`RabbitMQ API ${method} ${path} → ${res.status}: ${text}`);
  }
  if (res.status === 204) return null;
  return res.json();
}

const tools = [
  {
    name: "rabbitmq_list_queues",
    description: "List all queues in the vhost with message counts and consumer info.",
    inputSchema: {
      type: "object",
      properties: {
        vhost: { type: "string", description: "Virtual host (default: configured RABBITMQ_VHOST)" },
      },
    },
  },
  {
    name: "rabbitmq_get_queue",
    description: "Get details of a specific queue: messages, consumers, state, arguments.",
    inputSchema: {
      type: "object",
      properties: {
        queue: { type: "string", description: "Queue name" },
        vhost: { type: "string" },
      },
      required: ["queue"],
    },
  },
  {
    name: "rabbitmq_get_messages",
    description: "Peek (non-destructive) or get messages from a queue. Returns message payload, headers and properties.",
    inputSchema: {
      type: "object",
      properties: {
        queue: { type: "string", description: "Queue name" },
        count: { type: "number", description: "Max number of messages to retrieve (default 10)" },
        ackmode: {
          type: "string",
          enum: ["ack_requeue_true", "ack_requeue_false", "reject_requeue_true", "reject_requeue_false"],
          description: "ack_requeue_true = peek (non-destructive). Default: ack_requeue_true",
        },
        vhost: { type: "string" },
      },
      required: ["queue"],
    },
  },
  {
    name: "rabbitmq_publish_message",
    description: "Publish a message to an exchange (or default exchange for direct queue routing).",
    inputSchema: {
      type: "object",
      properties: {
        exchange: { type: "string", description: "Exchange name. Use '' for default exchange." },
        routing_key: { type: "string", description: "Routing key (queue name for default exchange)." },
        payload: { type: "string", description: "Message body as string." },
        properties: {
          type: "object",
          description: "AMQP properties (delivery_mode, content_type, headers, etc.)",
        },
        vhost: { type: "string" },
      },
      required: ["exchange", "routing_key", "payload"],
    },
  },
  {
    name: "rabbitmq_purge_queue",
    description: "Delete all messages from a queue (purge). Irreversible.",
    inputSchema: {
      type: "object",
      properties: {
        queue: { type: "string", description: "Queue name" },
        vhost: { type: "string" },
      },
      required: ["queue"],
    },
  },
  {
    name: "rabbitmq_list_exchanges",
    description: "List all exchanges in the vhost.",
    inputSchema: {
      type: "object",
      properties: {
        vhost: { type: "string" },
      },
    },
  },
  {
    name: "rabbitmq_list_bindings",
    description: "List bindings for a queue (which exchanges route to it).",
    inputSchema: {
      type: "object",
      properties: {
        queue: { type: "string", description: "Queue name" },
        vhost: { type: "string" },
      },
      required: ["queue"],
    },
  },
  {
    name: "rabbitmq_list_consumers",
    description: "List active consumers across all queues in the vhost.",
    inputSchema: {
      type: "object",
      properties: {
        vhost: { type: "string" },
      },
    },
  },
  {
    name: "rabbitmq_overview",
    description: "Get cluster overview: node info, message rates, queue/connection totals.",
    inputSchema: {
      type: "object",
      properties: {},
    },
  },
];

async function callTool(name, args) {
  const vhost = encodeURIComponent(args.vhost || VHOST);

  switch (name) {
    case "rabbitmq_list_queues": {
      const queues = await mgmt(`/queues/${vhost}`);
      return queues.map((q) => ({
        name: q.name,
        messages: q.messages,
        messages_ready: q.messages_ready,
        messages_unacknowledged: q.messages_unacknowledged,
        consumers: q.consumers,
        state: q.state,
        durable: q.durable,
        auto_delete: q.auto_delete,
      }));
    }

    case "rabbitmq_get_queue": {
      return mgmt(`/queues/${vhost}/${encodeURIComponent(args.queue)}`);
    }

    case "rabbitmq_get_messages": {
      const count = args.count ?? 10;
      const ackmode = args.ackmode ?? "ack_requeue_true";
      return mgmt(
        `/queues/${vhost}/${encodeURIComponent(args.queue)}/get`,
        "POST",
        { count, ackmode, encoding: "auto", truncate: 50000 }
      );
    }

    case "rabbitmq_publish_message": {
      const result = await mgmt(
        `/exchanges/${vhost}/${encodeURIComponent(args.exchange)}/publish`,
        "POST",
        {
          routing_key: args.routing_key,
          payload: args.payload,
          payload_encoding: "string",
          properties: args.properties ?? {},
        }
      );
      return result;
    }

    case "rabbitmq_purge_queue": {
      await mgmt(`/queues/${vhost}/${encodeURIComponent(args.queue)}/contents`, "DELETE");
      return { purged: true, queue: args.queue };
    }

    case "rabbitmq_list_exchanges": {
      const exchanges = await mgmt(`/exchanges/${vhost}`);
      return exchanges.map((e) => ({
        name: e.name || "(default)",
        type: e.type,
        durable: e.durable,
        auto_delete: e.auto_delete,
        internal: e.internal,
      }));
    }

    case "rabbitmq_list_bindings": {
      return mgmt(`/queues/${vhost}/${encodeURIComponent(args.queue)}/bindings`);
    }

    case "rabbitmq_list_consumers": {
      const consumers = await mgmt(`/consumers/${vhost}`);
      return consumers.map((c) => ({
        consumer_tag: c.consumer_tag,
        queue: c.queue?.name,
        channel: c.channel_details?.name,
        ack_required: c.ack_required,
        prefetch_count: c.prefetch_count,
        active: c.active,
      }));
    }

    case "rabbitmq_overview": {
      return mgmt("/overview");
    }

    default:
      throw new Error(`Unknown tool: ${name}`);
  }
}

const server = new Server(
  { name: "mcp-rabbitmq", version: "1.0.0" },
  { capabilities: { tools: {} } }
);

server.setRequestHandler(ListToolsRequestSchema, async () => ({ tools }));

server.setRequestHandler(CallToolRequestSchema, async (req) => {
  const { name, arguments: args } = req.params;
  try {
    const result = await callTool(name, args ?? {});
    return {
      content: [{ type: "text", text: JSON.stringify(result, null, 2) }],
    };
  } catch (err) {
    return {
      content: [{ type: "text", text: `Error: ${err.message}` }],
      isError: true,
    };
  }
});

const transport = new StdioServerTransport();
await server.connect(transport);
